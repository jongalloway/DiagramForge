using System.Text.Json;
using DiagramForge.Abstractions;
using DiagramForge.Icons;
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
    /// Icon registry used to resolve <see cref="Node.IconRef"/> references to
    /// <see cref="DiagramIcon"/> instances before rendering.
    /// </summary>
    public IconRegistry IconRegistry { get; } = new();

    /// <summary>
    /// Optional callback invoked when a non-fatal warning is raised during rendering
    /// (e.g. a missing icon pack). The argument is the complete warning message text.
    /// When <see langword="null"/> (the default) warnings are silently suppressed so
    /// that the library does not produce unsolicited console output.
    /// </summary>
    /// <example>
    /// Wire up to standard error in a CLI host:
    /// <code>
    /// var renderer = new DiagramRenderer();
    /// renderer.WarningHandler = msg => Console.Error.WriteLine(msg);
    /// </code>
    /// </example>
    public Action<string>? WarningHandler { get; set; }

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

        // Register built-in Mermaid architecture icons (cloud, database, disk, internet, server).
        IconRegistry.RegisterPack("builtin", new BuiltInArchitectureIconProvider());
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a named icon pack. Icons can then be referenced in diagrams as
    /// <c>packName:icon-name</c>.
    /// </summary>
    /// <returns>This renderer, for fluent chaining.</returns>
    public DiagramRenderer RegisterIconPack(string packName, IIconProvider provider)
    {
        IconRegistry.RegisterPack(packName, provider);
        return this;
    }

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
        => Render(diagramText, theme, paletteJson: null, transparentBackgroundOverride: null);

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
        => Render(diagramText, theme, paletteJson, transparentBackgroundOverride: null);

    /// <summary>
    /// Converts <paramref name="diagramText"/> to an SVG string, applying optional palette and
    /// transparent-background overrides on top of the resolved theme.
    /// </summary>
    public string Render(string diagramText, Theme? theme, string? paletteJson, bool? transparentBackgroundOverride)
    {
        var frontmatter = ParseFrontmatter(diagramText);
        var parser = FindParser(frontmatter.DiagramText)
            ?? throw new DiagramParseException(BuildUnknownParserMessage(frontmatter.DiagramText));

        var diagram = parser.Parse(frontmatter.DiagramText);
        var effectiveTheme = frontmatter.Theme ?? diagram.Theme ?? theme ?? _defaultTheme;
        var effectivePaletteJson = paletteJson ?? frontmatter.PaletteJson;
        bool? effectiveTransparentBackground = transparentBackgroundOverride ?? frontmatter.TransparentBackground;

        if (effectivePaletteJson is not null
            || frontmatter.BorderStyle is not null
            || frontmatter.FillStyle is not null
            || frontmatter.ShadowStyle is not null
            || effectiveTransparentBackground.HasValue
            || effectiveTheme.FillStyle is not null
            || effectiveTheme.ShadowStyle is not null)
            effectiveTheme = CloneTheme(effectiveTheme);

        if (effectiveTransparentBackground.HasValue)
        {
            effectiveTheme.TransparentBackground = effectiveTransparentBackground.Value;
        }

        if (effectiveTheme.FillStyle is not null)
        {
            ApplyFillStyle(effectiveTheme, effectiveTheme.FillStyle);
        }

        if (effectiveTheme.ShadowStyle is not null)
        {
            ApplyShadowStyle(effectiveTheme, effectiveTheme.ShadowStyle);
        }

        if (effectivePaletteJson is not null)
        {
            effectiveTheme.NodePalette = DeserializePaletteJson(effectivePaletteJson);
        }

        if (frontmatter.BorderStyle is not null)
        {
            ApplyBorderStyle(effectiveTheme, frontmatter.BorderStyle);
        }

        if (frontmatter.FillStyle is not null)
        {
            ApplyFillStyle(effectiveTheme, frontmatter.FillStyle);
        }

        if (frontmatter.ShadowStyle is not null)
        {
            ApplyShadowStyle(effectiveTheme, frontmatter.ShadowStyle);
        }

        if (frontmatter.EdgeRouting.HasValue)
        {
            diagram.LayoutHints.EdgeRouting = frontmatter.EdgeRouting.Value;
        }

        ResolveIcons(diagram);
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

    private void ResolveIcons(Diagram diagram)
    {
        bool warnedHeroicons = false;

        foreach (var node in diagram.Nodes.Values)
        {
            if (node.IconRef is not null)
            {
                var icon = IconRegistry.Resolve(node.IconRef);
                if (icon is not null)
                {
                    // Sanitize the SVG content before placing it into the output SVG
                    // to prevent XSS / injection from untrusted diagram text or icon providers.
                    string? sanitized = SvgIconSanitizer.Sanitize(icon.SvgContent);
                    node.ResolvedIcon = sanitized is not null
                        ? icon with { SvgContent = sanitized }
                        : null;
                }
                else if (!warnedHeroicons && IsHeroiconsReference(node.IconRef))
                {
                    warnedHeroicons = true;
                    WarningHandler?.Invoke(
                        $"""
                        Warning: Icon reference '{node.IconRef}' looks like a Heroicons icon, but the Heroicons pack is not registered.
                          Install the NuGet package and call .UseHeroicons():
                            dotnet add package DiagramForge.Icons.Heroicons
                            https://www.nuget.org/packages/DiagramForge.Icons.Heroicons
                        """);
                }
            }
        }
    }

    private static bool IsHeroiconsReference(string iconRef) =>
        iconRef.StartsWith("heroicons:", StringComparison.OrdinalIgnoreCase);

    private IDiagramParser? FindParser(string diagramText)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(diagramText))
                return parser;
        }
        return null;
    }

    private string BuildUnknownParserMessage(string diagramText)
    {
        string baseMessage =
            $"No registered parser can handle the supplied diagram text. " +
            $"Registered syntaxes: {string.Join(", ", _parsers.Select(p => p.SyntaxId))}";

        string? firstContentLine = GetFirstContentLine(diagramText);
        if (firstContentLine is null)
            return baseMessage;

        var hints = new List<string>();
        string normalized = firstContentLine.Trim();

        if (normalized.StartsWith("```", StringComparison.Ordinal))
        {
            hints.Add("Input appears to start with a Markdown code fence. Paste only the raw Mermaid or Conceptual DSL body, not the ```mermaid wrapper.");
        }

        string details = $" First content line: '{normalized}'.";
        if (hints.Count == 0)
            return baseMessage + details;

        return baseMessage + details + " Hint: " + string.Join(" ", hints);
    }

    private static string? GetFirstContentLine(string diagramText)
    {
        using var reader = new StringReader(diagramText);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("%%", StringComparison.Ordinal))
                continue;

            return trimmed;
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
                    $"paletteJson entry [{i}] \"{colors[i]}\" is not a valid hex color string (expected #RGB, #RGBA, #RRGGBB, or #RRGGBBAA).",
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
        PrimaryColor = source.PrimaryColor,
        SecondaryColor = source.SecondaryColor,
        AccentColor = source.AccentColor,
        BackgroundColor = source.BackgroundColor,
        SurfaceColor = source.SurfaceColor,
        BorderColor = source.BorderColor,
        NodeFillColor = source.NodeFillColor,
        NodeStrokeColor = source.NodeStrokeColor,
        GroupFillColor = source.GroupFillColor,
        GroupStrokeColor = source.GroupStrokeColor,
        EdgeColor = source.EdgeColor,
        TextColor = source.TextColor,
        TitleTextColor = source.TitleTextColor,
        SubtleTextColor = source.SubtleTextColor,
        FillStyle = source.FillStyle,
        ShadowStyle = source.ShadowStyle,
        BorderGradientStops = source.BorderGradientStops is null ? null : [.. source.BorderGradientStops],
        UseGradients = source.UseGradients,
        UseBorderGradients = source.UseBorderGradients,
        GradientStrength = source.GradientStrength,
        ShadowColor = source.ShadowColor,
        ShadowOpacity = source.ShadowOpacity,
        ShadowBlur = source.ShadowBlur,
        ShadowOffsetX = source.ShadowOffsetX,
        ShadowOffsetY = source.ShadowOffsetY,
        TransparentBackground = source.TransparentBackground,
        UseNodeShadows = source.UseNodeShadows,
        FontFamily = source.FontFamily,
        FontSize = source.FontSize,
        TitleFontSize = source.TitleFontSize,
        BorderRadius = source.BorderRadius,
        StrokeWidth = source.StrokeWidth,
        NodePadding = source.NodePadding,
        DiagramPadding = source.DiagramPadding,
        NodePalette = source.NodePalette is not null ? [.. source.NodePalette] : null,
        NodeStrokePalette = source.NodeStrokePalette is not null ? [.. source.NodeStrokePalette] : null,
    };

    private static FrontmatterOptions ParseFrontmatter(string raw)
    {
        if (!raw.StartsWith("---", StringComparison.Ordinal))
            return new FrontmatterOptions(raw, null, null, null, null, null, null);

        int endIndex = raw.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return new FrontmatterOptions(raw, null, null, null, null, null, null);

        string frontmatter = raw[3..endIndex].Trim();
        string diagramText = raw[(endIndex + 4)..].TrimStart('\r', '\n');

        Theme? parsedTheme = null;
        string? parsedPaletteJson = null;
        string? parsedBorderStyle = null;
        string? parsedFillStyle = null;
        string? parsedShadowStyle = null;
        bool? parsedTransparentBackground = null;
        EdgeRouting? parsedEdgeRouting = null;

        foreach (string rawLine in frontmatter.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("theme:", StringComparison.OrdinalIgnoreCase))
            {
                string name = Unquote(line["theme:".Length..].Trim());
                parsedTheme = Theme.GetByName(name)
                    ?? throw new ArgumentException($"Unknown theme name in frontmatter: '{name}'.", nameof(raw));
            }
            else if (line.StartsWith("palette:", StringComparison.OrdinalIgnoreCase))
            {
                parsedPaletteJson = line["palette:".Length..].Trim();
            }
            else if (line.StartsWith("borderStyle:", StringComparison.OrdinalIgnoreCase))
            {
                parsedBorderStyle = Unquote(line["borderStyle:".Length..].Trim());
            }
            else if (line.StartsWith("border-style:", StringComparison.OrdinalIgnoreCase))
            {
                parsedBorderStyle = Unquote(line["border-style:".Length..].Trim());
            }
            else if (line.StartsWith("fillStyle:", StringComparison.OrdinalIgnoreCase))
            {
                parsedFillStyle = Unquote(line["fillStyle:".Length..].Trim());
            }
            else if (line.StartsWith("fill-style:", StringComparison.OrdinalIgnoreCase))
            {
                parsedFillStyle = Unquote(line["fill-style:".Length..].Trim());
            }
            else if (line.StartsWith("shadowStyle:", StringComparison.OrdinalIgnoreCase))
            {
                parsedShadowStyle = Unquote(line["shadowStyle:".Length..].Trim());
            }
            else if (line.StartsWith("shadow-style:", StringComparison.OrdinalIgnoreCase))
            {
                parsedShadowStyle = Unquote(line["shadow-style:".Length..].Trim());
            }
            else if (line.StartsWith("transparent:", StringComparison.OrdinalIgnoreCase))
            {
                parsedTransparentBackground = ParseBoolean(line["transparent:".Length..].Trim(), raw, "transparent");
            }
            else if (line.StartsWith("transparentBackground:", StringComparison.OrdinalIgnoreCase))
            {
                parsedTransparentBackground = ParseBoolean(line["transparentBackground:".Length..].Trim(), raw, "transparentBackground");
            }
            else if (line.StartsWith("transparent-background:", StringComparison.OrdinalIgnoreCase))
            {
                parsedTransparentBackground = ParseBoolean(line["transparent-background:".Length..].Trim(), raw, "transparent-background");
            }
            else if (line.StartsWith("edgeRouting:", StringComparison.OrdinalIgnoreCase))
            {
                parsedEdgeRouting = ParseEdgeRouting(Unquote(line["edgeRouting:".Length..].Trim()), raw);
            }
            else if (line.StartsWith("edge-routing:", StringComparison.OrdinalIgnoreCase))
            {
                parsedEdgeRouting = ParseEdgeRouting(Unquote(line["edge-routing:".Length..].Trim()), raw);
            }
        }

        return new FrontmatterOptions(diagramText, parsedTheme, parsedPaletteJson, parsedBorderStyle, parsedFillStyle, parsedShadowStyle, parsedTransparentBackground, parsedEdgeRouting);
    }

    private static void ApplyBorderStyle(Theme theme, string borderStyle)
    {
        switch (borderStyle.Trim().ToLowerInvariant())
        {
            case "solid":
                theme.UseBorderGradients = false;
                theme.BorderGradientStops = null;
                break;
            case "subtle":
                theme.UseBorderGradients = true;
                theme.BorderGradientStops = null;
                break;
            case "rainbow":
                theme.UseBorderGradients = true;
                theme.BorderGradientStops = Theme.CreateExpressiveBorderStops(
                    theme.NodePalette ?? [theme.NodeFillColor, theme.PrimaryColor, theme.SecondaryColor, theme.AccentColor],
                    theme.BackgroundColor);
                break;
            default:
                throw new ArgumentException($"Unknown border style in frontmatter: '{borderStyle}'. Expected solid, subtle, or rainbow.", nameof(borderStyle));
        }
    }

    private static void ApplyFillStyle(Theme theme, string fillStyle)
    {
        switch (fillStyle.Trim().ToLowerInvariant())
        {
            case "flat":
                theme.FillStyle = "flat";
                theme.UseGradients = false;
                break;
            case "subtle":
                theme.FillStyle = "subtle";
                theme.UseGradients = true;
                theme.GradientStrength = Math.Min(Math.Max(theme.GradientStrength, 0.10), 0.12);
                break;
            case "diagonal-strong":
                theme.FillStyle = "diagonal-strong";
                theme.UseGradients = true;
                theme.GradientStrength = Math.Max(theme.GradientStrength, 0.16);
                break;
            default:
                throw new ArgumentException($"Unknown fill style in frontmatter: '{fillStyle}'. Expected flat, subtle, or diagonal-strong.", nameof(fillStyle));
        }
    }

    private static void ApplyShadowStyle(Theme theme, string shadowStyle)
    {
        switch (shadowStyle.Trim().ToLowerInvariant())
        {
            case "none":
                theme.ShadowStyle = "none";
                break;
            case "soft":
                theme.ShadowStyle = "soft";
                theme.ShadowOpacity = Math.Clamp(theme.ShadowOpacity <= 0 ? 0.12 : theme.ShadowOpacity, 0.04, 0.20);
                theme.ShadowBlur = Math.Clamp(theme.ShadowBlur <= 0 ? 1.20 : theme.ShadowBlur, 0.60, 2.40);
                theme.ShadowOffsetY = theme.ShadowOffsetY == 0 ? 1.20 : theme.ShadowOffsetY;
                break;
            case "glow":
                theme.ShadowStyle = "glow";
                theme.UseNodeShadows = true;
                // For glow, ensure a visible halo by bumping opacity/blur if unset,
                // clamping them to sensible ranges, and zeroing offsets so the
                // effect is centered around the node rather than offset like a drop shadow.
                theme.ShadowOpacity = Math.Clamp(theme.ShadowOpacity <= 0 ? 0.36 : theme.ShadowOpacity, 0.20, 0.80);
                theme.ShadowBlur = Math.Clamp(theme.ShadowBlur <= 0 ? 2.40 : theme.ShadowBlur, 1.20, 4.00);
                theme.ShadowOffsetX = 0;
                theme.ShadowOffsetY = 0;
                break;
            default:
                throw new ArgumentException($"Unknown shadow style in frontmatter: '{shadowStyle}'. Expected none, soft, or glow.", nameof(shadowStyle));
        }
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;

    private static EdgeRouting ParseEdgeRouting(string value, string raw) =>
        value.Trim().ToLowerInvariant() switch
        {
            "bezier" => EdgeRouting.Bezier,
            "orthogonal" => EdgeRouting.Orthogonal,
            "straight" => EdgeRouting.Straight,
            _ => throw new ArgumentException($"Unknown edge-routing value in frontmatter: '{value}'. Expected bezier, orthogonal, or straight.", nameof(raw)),
        };

    private static bool ParseBoolean(string rawValue, string raw, string fieldName)
    {
        string value = Unquote(rawValue.Trim());
        return value.ToLowerInvariant() switch
        {
            "true" or "yes" or "on" or "1" => true,
            "false" or "no" or "off" or "0" => false,
            _ => throw new ArgumentException($"Invalid boolean value for '{fieldName}' in frontmatter: '{value}'.", nameof(raw)),
        };
    }

    private sealed record FrontmatterOptions(string DiagramText, Theme? Theme, string? PaletteJson, string? BorderStyle, string? FillStyle, string? ShadowStyle, bool? TransparentBackground, EdgeRouting? EdgeRouting = null);
}

[System.Text.Json.Serialization.JsonSerializable(typeof(List<string>))]
internal partial class PaletteJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
