using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Radiomat;

internal sealed class EditionConfiguration
{
    internal static EditionConfiguration Current { get; } = Load();

    public string DisplayName { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string PublisherUrl { get; init; } = "";
    public string AppId { get; init; } = "";
    public string AppUserModelId { get; init; } = "";
    public string SettingsDirectory { get; init; } = "";
    public string RunValueName { get; init; } = "";
    public string InstanceName { get; init; } = "";
    public string SupportUri { get; init; } = "";
    public string UpdateLocation { get; init; } = "";
    public string UpdatePublicKey { get; init; } = "";
    public string CoffeeUri { get; init; } = "";
    public string CoffeeLabelPl { get; init; } = "";
    public string CoffeeLabelEn { get; init; } = "";
    public string CoffeeGlyph { get; init; } = "";
    public bool IsPublic { get; init; }

    private static EditionConfiguration Load()
    {
        // Read the build-time resource, never a replaceable file next to the EXE.
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Radiomat.Edition.json")
            ?? throw new InvalidDataException("Brak konfiguracji wydania.");
        return JsonSerializer.Deserialize<EditionConfiguration>(stream)
            ?? throw new InvalidDataException("Nieprawidlowa konfiguracja wydania.");
    }

    internal static void SetWindowsIdentity() => SetCurrentProcessExplicitAppUserModelID(Current.AppUserModelId);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
    private static extern void SetCurrentProcessExplicitAppUserModelID(string appId);
}
