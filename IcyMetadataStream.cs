using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Radiomat;

// Strip bounded ICY metadata blocks before passing MP3 bytes to the decoder.
internal sealed class IcyMetadataStream(Stream source, int interval, Action<string> titleChanged) : Stream
{
    private readonly byte[] _length = new byte[1];
    private readonly byte[] _metadata = new byte[255 * 16];
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Regex TitlePattern = new("(?:^|;)\\s*StreamTitle='(?<title>.*?)'(?:;|$)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private readonly int _interval = interval;
    private int _remaining = interval;
    private string? _lastTitle;

    static IcyMetadataStream() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.Length == 0) return 0;
        if (_remaining == 0)
        {
            await source.ReadExactlyAsync(_length.AsMemory(), cancellationToken);
            int length = _length[0] * 16;
            if (length != 0)
            {
                await source.ReadExactlyAsync(_metadata.AsMemory(0, length), cancellationToken);
                string text;
                try { text = StrictUtf8.GetString(_metadata, 0, length); }
                catch (DecoderFallbackException) { text = Encoding.GetEncoding(1250).GetString(_metadata, 0, length); }
                var match = TitlePattern.Match(text.TrimEnd('\0'));
                if (match.Success)
                {
                    var decoded = WebUtility.HtmlDecode(match.Groups["title"].Value);
                    string title = new(decoded.Where(c => !char.IsControl(c)).Take(300).ToArray());
                    if (title != _lastTitle) { _lastTitle = title; titleChanged(title); }
                }
            }
            if (_lastTitle == null)
            {
                _lastTitle = string.Empty;
                titleChanged(string.Empty);
            }
            _remaining = _interval;
        }
        int read = await source.ReadAsync(buffer[..Math.Min(buffer.Length, _remaining)], cancellationToken);
        _remaining -= read;
        return read;
    }

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
