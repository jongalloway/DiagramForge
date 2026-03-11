using System.Text.Json;
using DiagramForge.Abstractions;
using DiagramForge.Layout;
using DiagramForge.Models;
using DiagramForge.Parsers.Conceptual;
using DiagramForge.Parsers.Mermaid;
using DiagramForge.Rendering;

namespace DiagramForge;

/// <summary>
/// Primary entry point for converting diagram source text to SVG.
/// </summary>
/// <remarks>
/// <para>
/// <c>DiagramRenderer</c> wires together the parser registry, layout engine, and SVG renderer.
/// Callers supply raw diagram text; the renderer returns a complete SVG string.
/// </para>
/// <para>Usage example:</para>
/// <code>
/// var renderer = new DiagramRenderer();
/// string svg = renderer.Render("flowchart LR\n  A --> B");
/// </code>
/// </remarks>
public sealed class DiagramRenderer
{
    private readonly List<IDiagramParser> _parsers;
    private readonly ILayoutEngine _layoutEngine;
    private readonly ISvgRenderer _svgRenderer;
    private readonly Theme _defaultTheme;

    /// <summary>
    /// Creates a <see cref="DiagramRenderer"/> with the default parser set, layout engine, and theme.
    /// </summary>
    public DiagramRenderer()
        : this(
            parsers: [new MermaidParser(), new ConceptualDslParser()],
            layoutEngine: new DefaultLayoutEngine(),
            svgRenderer: new SvgRenderer(),
            defaultTheme: Theme.Default)
    {
    }

    /// <summary>
    /// Creates a <see cref="DiagramRenderer"/> with explicit dependencies (for testing / DI).
    /// </summary>
    public DiagramRenderer(
        IEnumerable<IDiagramParser> parsers,
        ILayoutEngine layoutEngine,
        ISvgRenderer svgRenderer,
        Theme defaultTheme)
    {
        ArgumentNullException.ThrowIfNull(parsers);
        ArgumentNullException.ThrowIfNull(layoutEngine);
        ArgumentNullException.ThrowIfNull(svgRenderer);
        ArgumentNullException.ThrowIfNull(defaultTheme);
        _parsers = [.. parsers];
        _layoutEngine = layoutEngine;
        _svgRenderer = svgRenderer;
        _defaultTheme = defaultTheme;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts <paramref name="diagramText"/> to an SVG string using the default theme.
    /// The correct parser is selected automatically based on the diagram text.
    /// </summary>
    /// <param name="diagramText">Raw diagram source text (e.g., Mermaid or Conceptual DSL).</param>
    /// <returns>A complete, self-contained SVG string.</returns>
    /// <exception cref="DiagramParseException">No registered parser can handle the input.</exception>
    public string Render(string diagramText) =>
        Render(diagramText, theme: null);

    /// <summary>
    /// Converts <paramref name="diagramText"/> to an SVG string using the supplied <paramref name="theme"/>.
    /// </summary>
    public string Render(string diagramText, Theme? theme)
        => Render(diagramText, theme, paletteJson: null);

    /// <summary>
    /// Converts <paramref name="diagramText"/> to an SVG string, applying an optional JSON palette
    /// override on top of the supplied <paramref name="theme"/>.
    /// </summary>
    /// <param name="diagramText">Raw diagram source text.</param>
    /// <param name="theme">Theme to use, or <see langword="null"/> for the default theme.</param>
    /// <param name="paletteJson">
    /// An optional JSON array of hex color strings, e.g. <c>["#FF0000","#00FF00"]</c>.
    /// When provided, these colors replace the <see cref="Theme.NodePalette"/> of the effective
    /// theme while leaving all other theme properties unchanged.
    /// Invalid JSON or values that are not hex color strings result in an
    /// <see cref="ArgumentException"/>.
    /// </param>
    /// <returns>A complete, self-contained SVG string.</returns>
    /// <exception cref="DiagramParseException">No registered parser can handle the input.</exception>
    /// <exception cref="ArgumentException"><paramref name="paletteJson"/> is not valid JSON or contains non-hex values.</exception>
    public string Render(string diagramText, Theme? theme, string? paletteJson)
    {
        var parser = FindParser(diagramText)
            ?? throw new DiagramParseException(
                $"No registered parser can handle the supplied diagram text. " +
                $"Registered syntaxes: {string.Join(", ", _parsers.Select(p => p.SyntaxId))}");

        var diagram = parser.Parse(diagramText);
        var effectiveTheme = diagram.Theme ?? theme ?? _defaultTheme;

        if (paletteJson is not null)
        {
            // Clone so we don't mutate shared instances like Theme.Dark
            effectiveTheme = CloneTheme(effectiveTheme);
            effectiveTheme.NodePalette = DeserializePaletteJson(paletteJson);
        }

        _layoutEngine.Layout(diagram, effectiveTheme);
        return _svgRenderer.Render(diagram, effectiveTheme);
    }

    /// <summary>
    /// Registers an additional parser. The parser is tried before the built-in parsers.
    /// </summary>
    public DiagramRenderer RegisterParser(IDiagramParser parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        _parsers.Insert(0, parser);
        return this;
    }

    /// <summary>
    /// Returns all registered parser syntax IDs.
    /// </summary>
    public IReadOnlyList<string> RegisteredSyntaxes =>
        _parsers.Select(p => p.SyntaxId).ToList().AsReadOnly();

    // ── Private helpers ───────────────────────────────────────────────────────

    private IDiagramParser? FindParser(string diagramText)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(diagramText))
                return parser;
        }
        return null;
    }

    private static List<string> DeserializePaletteJson(string paletteJson)
    {
        List<string>? colors;
        try
        {
            colors = JsonSerializer.Deserialize(paletteJson, PaletteJsonContext.Default.ListString);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"paletteJson is not valid JSON: {ex.Message}", nameof(paletteJson), ex);
        }

        if (colors is null || colors.Count == 0)
            throw new ArgumentException(
                "paletteJson must be a non-empty JSON array of hex color strings.",
                nameof(paletteJson));

        for (int i = 0; i < colors.Count; i++)
        {
            string color = colors[i]?.Trim() ?? string.Empty;
            if (!IsHexColor(color))
                throw new ArgumentException(
                    $"paletteJson entry [{i}] \"{colors[i]}\" is not a valid hex color string (expected #RGB or #RRGGBB).",
                    nameof(paletteJson));
            colors[i] = color;
        }

        return colors;
    }

    private static bool IsHexColor(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] != '#')
            return false;
        string hex = value[1..];
        return (hex.Length == 3 || hex.Length == 4 || hex.Length == 6 || hex.Length == 8)
            && hex.All(c => Uri.IsHexDigit(c));
    }

    private static Theme CloneTheme(Theme source) => new()
    {
        PrimaryColor     = source.PrimaryColor,
        SecondaryColor   = source.SecondaryColor,
        AccentColor      = source.AccentColor,
        BackgroundColor  = source.BackgroundColor,
        NodeFillColor    = source.NodeFillColor,
        NodeStrokeColor  = source.NodeStrokeColor,
        EdgeColor        = source.EdgeColor,
        TextColor        = source.TextColor,
        SubtleTextColor  = source.SubtleTextColor,
        FontFamily       = source.FontFamily,
        FontSize         = source.FontSize,
        TitleFontSize    = source.TitleFontSize,
        BorderRadius     = source.BorderRadius,
        StrokeWidth      = source.StrokeWidth,
        NodePadding      = source.NodePadding,
        DiagramPadding   = source.DiagramPadding,
        NodePalette      = source.NodePalette is not null ? [.. source.NodePalette] : null,
        NodeStrokePalette = source.NodeStrokePalette is not null ? [.. source.NodeStrokePalette] : null,
    };
}

[System.Text.Json.Serialization.JsonSerializable(typeof(List<string>))]
internal partial class PaletteJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
