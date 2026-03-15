using System.Text.Json;
using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Icons.Heroicons;

/// <summary>
/// Provides Heroicons (outline, 24×24) as a DiagramForge icon pack.
/// Icons are loaded from an embedded JSON resource on first access.
/// </summary>
public sealed class HeroiconsProvider : IIconProvider
{
    private const string ViewBox = "0 0 24 24";
    private readonly Lazy<Dictionary<string, string>> _icons = new(LoadIcons);

    /// <inheritdoc/>
    public DiagramIcon? GetIcon(string name)
    {
        if (_icons.Value.TryGetValue(name, out var svgContent))
            return new DiagramIcon("heroicons", name, ViewBox, svgContent);

        return null;
    }

    /// <inheritdoc/>
    public IEnumerable<string> AvailableIcons => _icons.Value.Keys;

    private static Dictionary<string, string> LoadIcons()
    {
        var assembly = typeof(HeroiconsProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream("heroicons.json")
            ?? throw new InvalidOperationException("Heroicons embedded resource not found.");

        var data = JsonSerializer.Deserialize(stream, HeroiconsJsonContext.Default.DictionaryStringString)
            ?? throw new InvalidOperationException("Failed to deserialize heroicons data.");

        return new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class HeroiconsJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
