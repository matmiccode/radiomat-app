using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Radiomat;

internal static class StationCatalog
{
    public static IReadOnlyList<Station> Load(IEnumerable<Station> defaults)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Radiomat.Data.poland.json")
            ?? throw new InvalidDataException("Brak katalogu stacji w programie.");
        var bundled = JsonSerializer.Deserialize<Station[]>(stream) ?? [];
        // Keep the three existing, user-confirmed streams as the default shortcuts.
        return defaults.Concat(bundled).DistinctBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pl-PL"), true)).ToArray();
    }
}
