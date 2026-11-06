using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NSec.Cryptography;

namespace Radiomat;

internal sealed record UpdateOffer(Version Version, string FileName, long Size, string Notes,
    string Installer, string Signature, string Checksum);

internal sealed class UpdateService : IDisposable
{
    internal static Version CurrentVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version!;
    internal static string VersionText => CurrentVersion.ToString(3);
    private const int MaxInstallerBytes = 256 * 1024 * 1024;
    private readonly EditionConfiguration _edition = EditionConfiguration.Current;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(90), MaxResponseContentBufferSize = 128 * 1024 };
    private readonly CancellationTokenSource _stop = new();
    internal UpdateService() => _http.DefaultRequestHeaders.UserAgent.ParseAdd("radioMAT/" + VersionText);

    internal async Task<UpdateOffer?> CheckAsync()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        return await Task.Run(async () =>
        {
            if (_edition.IsPublic)
            {
                using var response = await _http.GetAsync(_edition.UpdateLocation, deadline.Token);
                if (response.StatusCode == HttpStatusCode.NotFound) return null;
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(deadline.Token);
                if (bytes.Length > 128 * 1024) throw new InvalidDataException();
                using var doc = JsonDocument.Parse(bytes);
                var root = doc.RootElement;
                if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
                var version = ParseVersion(root.GetProperty("tag_name").GetString()?.TrimStart('v'));
                var name = InstallerName(version);
                var assets = root.GetProperty("assets").EnumerateArray().ToArray();
                JsonElement Asset(string file) => assets.Single(a => a.GetProperty("name").GetString() == file);
                string Url(string file)
                {
                    var url = Asset(file).GetProperty("browser_download_url").GetString()!;
                    // Only this repository's release directory. Redirects used by GitHub's CDN
                    // are permitted by HttpClient; downloaded bytes still require our signature.
                    var api = new Uri(_edition.UpdateLocation);
                    var repo = api.AbsolutePath.Split('/');
                    var expected = $"https://github.com/{repo[2]}/{repo[3]}/releases/download/";
                    if (!url.StartsWith(expected, StringComparison.Ordinal) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                        || uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
                        || Uri.UnescapeDataString(uri.Segments[^1]) != file) throw new InvalidDataException();
                    return url;
                }
                return Validate(new UpdateOffer(version, name, Asset(name).GetProperty("size").GetInt64(),
                    LimitNotes(root.GetProperty("body").GetString()), Url(name), Url(name + ".podpis"), Url(name + ".sha256")));
            }
            else
            {
                var path = Path.Combine(_edition.UpdateLocation, "najnowsza.json");
                var bytes = await ReadLocalAsync(path, 64 * 1024, deadline.Token);
                using var doc = JsonDocument.Parse(bytes);
                var root = doc.RootElement;
                if (root.GetProperty("program").GetString() != _edition.DisplayName) throw new InvalidDataException();
                var version = ParseVersion(root.GetProperty("wersja").GetString());
                var name = root.GetProperty("instalator").GetString();
                if (name != InstallerName(version)) throw new InvalidDataException();
                return Validate(new UpdateOffer(version, name, root.GetProperty("rozmiar").GetInt64(),
                    LimitNotes(root.GetProperty("opis").GetString()), Path.Combine(_edition.UpdateLocation, name),
                    Path.Combine(_edition.UpdateLocation, name + ".podpis"), Path.Combine(_edition.UpdateLocation, name + ".sha256")));
            }
        }, deadline.Token);
    }

    private static UpdateOffer Validate(UpdateOffer offer)
    {
        if (offer.Size < 1024 || offer.Size > MaxInstallerBytes) throw new InvalidDataException();
        return offer;
    }
    private static string LimitNotes(string? notes) => (notes ?? "").Length <= 12000 ? notes ?? "" : notes![..12000];
    private static string InstallerName(Version version) => $"radioMAT-Setup-{version.ToString(3)}-x64.exe";
    private static Version ParseVersion(string? value)
    {
        if (value == null || !Regex.IsMatch(value, @"^\d{1,4}\.\d{1,4}\.\d{1,4}$") || !Version.TryParse(value, out var version))
            throw new InvalidDataException();
        return new Version(version.Major, version.Minor, version.Build, 0);
    }
    private static async Task<byte[]> ReadLocalAsync(string path, int max, CancellationToken token)
    {
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (source.Length > max) throw new InvalidDataException();
        using var memory = new MemoryStream();
        await CopyBoundedAsync(source, memory, max, token);
        return memory.ToArray();
    }
    private async Task<byte[]> ReadSmallAsync(string source, CancellationToken token)
    {
        if (!_edition.IsPublic) return await ReadLocalAsync(source, 4096, token);
        using var response = await _http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var memory = new MemoryStream();
        await CopyBoundedAsync(stream, memory, 4096, token);
        return memory.ToArray();
    }
    private static async Task CopyBoundedAsync(Stream source, Stream target, long limit, CancellationToken token)
    {
        var buffer = new byte[65536];
        long total = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, token)) != 0)
        {
            total += count;
            if (total > limit) throw new InvalidDataException();
            await target.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }

    // Returns a private staging directory only after signature, checksum, file name,
    // size and installer product version agree. No executable is started here.
    internal async Task<(string Path, string Hash)> DownloadAsync(UpdateOffer offer)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        deadline.CancelAfter(TimeSpan.FromMinutes(4));
        return await Task.Run(async () =>
        {
            if (offer.Version <= CurrentVersion) throw new InvalidDataException();
            var directory = Path.Combine(Path.GetTempPath(), "radioMAT-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, offer.FileName);
            try
            {
                var signatureText = Encoding.UTF8.GetString(await ReadSmallAsync(offer.Signature, deadline.Token)).Trim();
                var checksumText = Encoding.UTF8.GetString(await ReadSmallAsync(offer.Checksum, deadline.Token)).Trim();
                var checksum = checksumText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (checksum.Length != 2 || checksum[1] != offer.FileName || !Regex.IsMatch(checksum[0], "^[a-fA-F0-9]{64}$"))
                    throw new InvalidDataException();
                var signature = Convert.FromBase64String(signatureText);
                if (signature.Length != 64) throw new InvalidDataException();
                using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                {
                    if (_edition.IsPublic)
                    {
                        using var response = await _http.GetAsync(offer.Installer, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                        response.EnsureSuccessStatusCode();
                        using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
                        await CopyBoundedAsync(input, output, offer.Size, deadline.Token);
                    }
                    else
                    {
                        using var input = new FileStream(offer.Installer, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                        await CopyBoundedAsync(input, output, offer.Size, deadline.Token);
                    }
                }
                var bytes = await File.ReadAllBytesAsync(target, deadline.Token);
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                var algorithm = SignatureAlgorithm.Ed25519;
                var key = PublicKey.Import(algorithm, Convert.FromBase64String(_edition.UpdatePublicKey), KeyBlobFormat.RawPublicKey);
                if (bytes.LongLength != offer.Size || !hash.Equals(checksum[0], StringComparison.OrdinalIgnoreCase)
                    || !algorithm.Verify(key, bytes, signature)) throw new InvalidDataException();
                var info = FileVersionInfo.GetVersionInfo(target);
                var actual = new Version(info.ProductMajorPart, info.ProductMinorPart, info.ProductBuildPart, info.ProductPrivatePart);
                if (actual != offer.Version) throw new InvalidDataException();
                return (target, hash);
            }
            catch
            {
                try { File.Delete(target); Directory.Delete(directory); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
        }, deadline.Token);
    }

    internal static void LaunchInstaller(string installer, string hash)
    {
        // Encoded string values avoid PowerShell quoting/injection issues for account
        // names containing spaces, apostrophes, non-ASCII or metacharacters.
        static string Enc(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        var executable = Environment.ProcessPath ?? throw new IOException();
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            function Decode($s) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($s)) }
            $installer = Decode '{{Enc(installer)}}'
            $app = Decode '{{Enc(executable)}}'
            $lock = $null
            $ok = $false
            try {
                $parent = Get-Process -Id {{Environment.ProcessId}} -ErrorAction SilentlyContinue
                if ($parent -and -not $parent.WaitForExit(60000)) { exit 2 }
                $lock = [IO.File]::Open($installer, 'Open', 'Read', 'Read')
                $sha = [Security.Cryptography.SHA256]::Create()
                try { $actual = [BitConverter]::ToString($sha.ComputeHash($lock)).Replace('-', '') } finally { $sha.Dispose() }
                if ($actual -cne '{{hash}}') { throw 'Installer changed after verification.' }
                $process = Start-Process -FilePath $installer -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /NORESTARTAPPLICATIONS' -PassThru -Wait
                $ok = $process.ExitCode -eq 0 -or $process.ExitCode -eq 3010
            } catch { $ok = $false }
            finally { if ($lock) { $lock.Dispose() } }
            if ($ok) { Start-Process -FilePath $app }
            else { Start-Process -FilePath $app -ArgumentList '--update-failed' }
            Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath ([IO.Path]::GetDirectoryName($installer)) -ErrorAction SilentlyContinue
            """;
        var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        var start = new ProcessStartInfo(powershell) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden", "-ExecutionPolicy", "Bypass", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) })
            start.ArgumentList.Add(argument);
        using var helper = Process.Start(start) ?? throw new IOException();
    }

    public void Dispose() { _stop.Cancel(); _http.Dispose(); }
}
