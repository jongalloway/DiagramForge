using System.Text.Json;

namespace DiagramForge.Models;

/// <summary>
/// Defines the visual theme (colors, fonts, sizing) applied to a rendered diagram.
/// </summary>
public class Theme
{
    // ── Built-in named presets ────────────────────────────────────────────────

    /// <summary>Standard colorful theme (matches Mermaid's <c>default</c> theme).</summary>
    public static Theme Default => new();

    /// <summary>Dark-mode optimized theme (matches Mermaid's <c>dark</c> theme).</summary>
    public static Theme Dark => new()
    {
        PrimaryColor   = "#5B9BD5",
        SecondaryColor = "#70AD47",
        AccentColor    = "#ED7D31",
        BackgroundColor = "#1E1E1E",
        NodeFillColor   = "#2D3748",
        NodeStrokeColor = "#5B9BD5",
        EdgeColor       = "#A0AEC0",
        TextColor       = "#E2E8F0",
        SubtleTextColor = "#A0AEC0",
        NodePalette =
        [
            "#2B4A7A", "#1D5C37", "#7A3A1A", "#5A2D82", "#1A5C5C", "#6B2D2D",
            "#2D4A6B", "#1A4D2E",
        ],
    };

    /// <summary>Black-and-white friendly theme suitable for printing (matches Mermaid's <c>neutral</c> theme).</summary>
    public static Theme Neutral => new()
    {
        PrimaryColor    = "#555555",
        SecondaryColor  = "#888888",
        AccentColor     = "#333333",
        BackgroundColor = "#FFFFFF",
        NodeFillColor   = "#F5F5F5",
        NodeStrokeColor = "#555555",
        EdgeColor       = "#555555",
        TextColor       = "#111111",
        SubtleTextColor = "#666666",
        NodePalette =
        [
            "#E8E8E8", "#D0D0D0", "#B8B8B8", "#A0A0A0", "#888888", "#707070",
            "#D8D8D8", "#C0C0C0",
        ],
    };

    /// <summary>Green palette theme (matches Mermaid's <c>forest</c> theme).</summary>
    public static Theme Forest => new()
    {
        PrimaryColor    = "#34713F",
        SecondaryColor  = "#52A563",
        AccentColor     = "#8BC34A",
        BackgroundColor = "#F9FFF9",
        NodeFillColor   = "#D7F3DA",
        NodeStrokeColor = "#34713F",
        EdgeColor       = "#34713F",
        TextColor       = "#1B3A20",
        SubtleTextColor = "#52A563",
        NodePalette =
        [
            "#C8E6C9", "#A5D6A7", "#81C784", "#66BB6A", "#4CAF50", "#43A047",
            "#B2DFDB", "#80CBC4",
        ],
    };

    /// <summary>High-contrast presentation theme with larger fonts and generous spacing.</summary>
    public static Theme Presentation => new()
    {
        PrimaryColor    = "#003087",
        SecondaryColor  = "#D62828",
        AccentColor     = "#F4A261",
        BackgroundColor = "#FFFFFF",
        NodeFillColor   = "#D0E4FF",
        NodeStrokeColor = "#003087",
        EdgeColor       = "#003087",
        TextColor       = "#000000",
        SubtleTextColor = "#333333",
        FontSize        = 16,
        TitleFontSize   = 20,
        NodePadding     = 16,
        DiagramPadding  = 32,
        BorderRadius    = 10,
        NodePalette =
        [
            "#D0E4FF", "#FFD6D6", "#FFF3CD", "#D4EDDA", "#E2D9F3", "#D1ECF1",
            "#FDECEA", "#E8F5E9",
        ],
    };

    // ── Palette lookup ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a built-in <see cref="Theme"/> by name (case-insensitive), or <see langword="null"/>
    /// for an unrecognised name.
    /// </summary>
    /// <param name="name">One of: <c>default</c>, <c>dark</c>, <c>neutral</c>, <c>forest</c>, <c>presentation</c>.</param>
    public static Theme? GetByName(string? name) =>
        name?.Trim().ToLowerInvariant() switch
        {
            "default"      => Default,
            "dark"         => Dark,
            "neutral"      => Neutral,
            "forest"       => Forest,
            "presentation" => Presentation,
            _              => null,
        };

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="Theme"/> by deriving all colors from a set of base palette entries.
    /// Missing parameters fall back to sensible defaults derived from <paramref name="primaryColor"/>.
    /// </summary>
    /// <param name="primaryColor">Main accent color (required), e.g. <c>#4F81BD</c>.</param>
    /// <param name="secondaryColor">Secondary accent (optional).</param>
    /// <param name="accentColor">Third accent (optional).</param>
    /// <param name="backgroundColor">Canvas background (optional, defaults to <c>#FFFFFF</c>).</param>
    public static Theme FromPalette(
        string primaryColor,
        string? secondaryColor = null,
        string? accentColor = null,
        string? backgroundColor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryColor);

        string bg       = backgroundColor ?? "#FFFFFF";
        string primary  = primaryColor;
        string secondary = secondaryColor ?? ColorUtils.Lighten(primary, 0.25);
        string accent    = accentColor    ?? ColorUtils.Darken(primary, 0.15);

        string nodeFill   = ColorUtils.Lighten(primary, 0.60);
        string nodeStroke = ColorUtils.Darken(primary, 0.15);
        string edgeColor  = ColorUtils.Darken(primary, 0.20);
        string textColor  = IsLightColor(bg) ? "#1F2937" : "#E2E8F0";
        string subtleText = IsLightColor(bg) ? "#6B7280" : "#A0AEC0";

        var palette = new List<string>
        {
            ColorUtils.Lighten(primary,   0.55),
            ColorUtils.Lighten(secondary, 0.55),
            ColorUtils.Lighten(accent,    0.55),
            ColorUtils.Lighten(primary,   0.35),
            ColorUtils.Lighten(secondary, 0.35),
            ColorUtils.Lighten(accent,    0.35),
            ColorUtils.Lighten(primary,   0.70),
            ColorUtils.Lighten(secondary, 0.70),
        };

        return new Theme
        {
            PrimaryColor    = primary,
            SecondaryColor  = secondary,
            AccentColor     = accent,
            BackgroundColor = bg,
            NodeFillColor   = nodeFill,
            NodeStrokeColor = nodeStroke,
            EdgeColor       = edgeColor,
            TextColor       = textColor,
            SubtleTextColor = subtleText,
            NodePalette     = palette,
        };
    }

    // ── JSON serialization ────────────────────────────────────────────────────

    /// <summary>Serializes this theme to a JSON string.</summary>
    public string ToJson() =>
        JsonSerializer.Serialize(this, ThemeJsonContext.Default.Theme);

    /// <summary>Deserializes a <see cref="Theme"/> from a JSON string.</summary>
    /// <exception cref="ArgumentException">The JSON is invalid or cannot be deserialized.</exception>
    public static Theme FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            return JsonSerializer.Deserialize(json, ThemeJsonContext.Default.Theme)
                   ?? throw new ArgumentException("JSON deserialized to null.", nameof(json));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid theme JSON: {ex.Message}", nameof(json), ex);
        }
    }

    // ── Palette properties ────────────────────────────────────────────────────

    /// <summary>
    /// An ordered list of hex fill colors the renderer cycles through for nodes that do not have
    /// an explicit <see cref="Node.FillColor"/> set. When <see langword="null"/> or empty, the
    /// renderer falls back to <see cref="NodeFillColor"/> for all nodes.
    /// </summary>
    public List<string>? NodePalette { get; set; }

    /// <summary>
    /// An optional matching stroke palette cycled alongside <see cref="NodePalette"/>.
    /// When <see langword="null"/>, stroke colors are derived by darkening the corresponding
    /// <see cref="NodePalette"/> entry.
    /// </summary>
    public List<string>? NodeStrokePalette { get; set; }

    // ── Color properties ──────────────────────────────────────────────────────

    public string PrimaryColor { get; set; } = "#4F81BD";
    public string SecondaryColor { get; set; } = "#70AD47";
    public string AccentColor { get; set; } = "#ED7D31";
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string NodeFillColor { get; set; } = "#DAE8FC";
    public string NodeStrokeColor { get; set; } = "#4F81BD";
    public string EdgeColor { get; set; } = "#555555";
    public string TextColor { get; set; } = "#1F2937";
    public string SubtleTextColor { get; set; } = "#6B7280";

    // ── Typography ────────────────────────────────────────────────────────────
    public string FontFamily { get; set; } = "\"Segoe UI\", Inter, Arial, sans-serif";
    public double FontSize { get; set; } = 13;
    public double TitleFontSize { get; set; } = 15;

    // ── Shape & Spacing ───────────────────────────────────────────────────────
    public double BorderRadius { get; set; } = 8;
    public double StrokeWidth { get; set; } = 1.5;
    public double NodePadding { get; set; } = 12;
    public double DiagramPadding { get; set; } = 24;

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool IsLightColor(string hex)
    {
        try
        {
            (int r, int g, int b) = ColorUtils.ParseHex(hex);
            double luminance = r * 0.299 + g * 0.587 + b * 0.114;
            return luminance > 128;
        }
        catch
        {
            return true; // default: treat as light
        }
    }
}
