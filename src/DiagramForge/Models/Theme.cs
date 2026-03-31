using System.Text.Json;

namespace DiagramForge.Models;

/// <summary>
/// Defines the visual theme (colors, fonts, sizing) applied to a rendered diagram.
/// </summary>
public class Theme
{
    private static readonly string[] BuiltInThemeNameArray =
    [
        "default",
        "zinc-light",
        "zinc-dark",
        "dark",
        "neutral",
        "forest",
        "presentation",
        "prism",
        "angled-light",
        "angled-dark",
        "github-light",
        "github-dark",
        "nord",
        "nord-light",
        "dracula",
        "tokyo-night",
        "tokyo-night-storm",
        "tokyo-night-light",
        "catppuccin-latte",
        "catppuccin-mocha",
        "solarized-light",
        "solarized-dark",
        "one-dark",
        "cyberpunk",
        "synthwave",
        "glass",
        "neumorphic",
        "neon",
    ];

    // ── Built-in named presets ────────────────────────────────────────────────

    /// <summary>Standard colorful theme (matches Mermaid's <c>default</c> theme).</summary>
    public static Theme Default => CreatePreset(
        backgroundColor: "#FCFCFD",
        foregroundColor: "#0F172A",
        accentColor: "#2563EB",
        mutedColor: "#667085",
        surfaceColor: "#EAF2FF",
        borderColor: "#6C8FF5",
        lineColor: "#516074",
        nodePalette:
        [
            "#EAF2FF", "#E8F7F0", "#FFF1E7", "#F5EAFE",
            "#E6F7F7", "#FFF0F3", "#EEF2FF", "#F7F4EA",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.10);

    /// <summary>Minimal zinc light theme aligned with Beautiful Mermaid's defaults.</summary>
    public static Theme ZincLight => CreatePreset(
        backgroundColor: "#FFFFFF",
        foregroundColor: "#27272A",
        accentColor: "#3B82F6",
        mutedColor: "#71717A",
        surfaceColor: "#F5F5F5",
        borderColor: "#A1A1AA",
        lineColor: "#71717A",
        nodePalette:
        [
            "#F7F7F8", "#F1F1F3", "#EBECF0", "#F4F4F7",
            "#ECECF2", "#E8E8EE", "#F3F4F6", "#EDEEFA",
        ],
        useGradients: true,
        useBorderGradients: false,
        gradientStrength: 0.08);

    /// <summary>Minimal zinc dark theme aligned with Beautiful Mermaid's dark neutral preset.</summary>
    public static Theme ZincDark => CreatePreset(
        backgroundColor: "#18181B",
        foregroundColor: "#FAFAFA",
        accentColor: "#60A5FA",
        mutedColor: "#A1A1AA",
        surfaceColor: "#27272A",
        borderColor: "#52525B",
        lineColor: "#52525B",
        nodePalette:
        [
            "#232326", "#2A2A30", "#303039", "#26262C",
            "#2D2E36", "#343540", "#2A3040", "#233243",
        ],
        useGradients: true,
        useBorderGradients: false,
        gradientStrength: 0.08);

    /// <summary>Dark-mode optimized theme (matches Mermaid's <c>dark</c> theme).</summary>
    public static Theme Dark => CreatePreset(
        backgroundColor: "#111827",
        foregroundColor: "#E5EEF9",
        accentColor: "#7DD3FC",
        mutedColor: "#8FA3B8",
        surfaceColor: "#1F2937",
        borderColor: "#38BDF8",
        lineColor: "#94A3B8",
        nodePalette:
        [
            "#243B53", "#183C4F", "#314E89", "#234E52",
            "#4C3F91", "#4A5568", "#3B4252", "#2F4858",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.10);

    /// <summary>Black-and-white friendly theme suitable for printing (matches Mermaid's <c>neutral</c> theme).</summary>
    public static Theme Neutral => CreatePreset(
        backgroundColor: "#FFFFFF",
        foregroundColor: "#111111",
        accentColor: "#4B5563",
        mutedColor: "#6B7280",
        surfaceColor: "#F3F4F6",
        borderColor: "#9CA3AF",
        lineColor: "#4B5563",
        nodePalette:
        [
            "#F5F5F5", "#ECECEC", "#E3E3E3", "#D8D8D8",
            "#CDCDCD", "#BFBFBF", "#EAEAEA", "#D2D2D2",
        ],
        useGradients: true,
        useBorderGradients: false,
        gradientStrength: 0.08);

    /// <summary>Green palette theme (matches Mermaid's <c>forest</c> theme).</summary>
    public static Theme Forest => CreatePreset(
        backgroundColor: "#F6FBF5",
        foregroundColor: "#16311F",
        accentColor: "#2F855A",
        mutedColor: "#5B7F69",
        surfaceColor: "#DFF3E5",
        borderColor: "#2F855A",
        lineColor: "#406B4F",
        nodePalette:
        [
            "#DFF3E5", "#CBEACF", "#B6E1BE", "#A3D8B0",
            "#D7F0ED", "#E8F6D3", "#CCEBDD", "#BDE6D0",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.10,
        useMultiStopBorderGradient: true);

    /// <summary>High-contrast presentation theme with larger fonts and generous spacing.</summary>
    public static Theme Presentation => CreatePresentationTheme();

    /// <summary>White-fill theme where multi-stop borders carry the visual emphasis.</summary>
    public static Theme Prism => CreatePrismTheme();

    /// <summary>Light theme with noticeably stronger diagonal fills and restrained borders.</summary>
    public static Theme AngledLight => CreateAngledLightTheme();

    /// <summary>Dark theme where diagonal fill modulation carries the primary visual emphasis.</summary>
    public static Theme AngledDark => CreateAngledDarkTheme();

    public static Theme GithubLight => CreatePreset(
        backgroundColor: "#FFFFFF",
        foregroundColor: "#1F2328",
        accentColor: "#0969DA",
        mutedColor: "#59636E",
        surfaceColor: "#F6F8FA",
        borderColor: "#D0D7DE",
        lineColor: "#57606A",
        nodePalette:
        [
            "#EAF2FF", "#E8F4EA", "#FFF0E5", "#F2ECFF",
            "#E5F7F7", "#FFF0F4", "#EEF2F7", "#F8F1E8",
        ],
        useGradients: true,
        useBorderGradients: false,
        gradientStrength: 0.10);

    public static Theme GithubDark => CreatePreset(
        backgroundColor: "#0D1117",
        foregroundColor: "#E6EDF3",
        accentColor: "#4493F8",
        mutedColor: "#9198A1",
        surfaceColor: "#161B22",
        borderColor: "#3D444D",
        lineColor: "#8B949E",
        nodePalette:
        [
            "#172033", "#15212B", "#1F2A44", "#20313F",
            "#2A1F44", "#2B2F36", "#1E2635", "#1F2B2F",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.11,
        useMultiStopBorderGradient: true);

    public static Theme Nord => CreatePreset(
        backgroundColor: "#2E3440",
        foregroundColor: "#D8DEE9",
        accentColor: "#88C0D0",
        mutedColor: "#A7B5C8",
        surfaceColor: "#3B4252",
        borderColor: "#5E81AC",
        lineColor: "#81A1C1",
        nodePalette:
        [
            "#434C5E", "#4C566A", "#5E81AC", "#81A1C1",
            "#88C0D0", "#8FBCBB", "#53657D", "#43536A",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.09);

    public static Theme NordLight => CreatePreset(
        backgroundColor: "#ECEFF4",
        foregroundColor: "#2E3440",
        accentColor: "#5E81AC",
        mutedColor: "#7B88A1",
        surfaceColor: "#E5EAF1",
        borderColor: "#AAB1C0",
        lineColor: "#AAB1C0",
        nodePalette:
        [
            "#E6EAF0", "#DEE4EC", "#D7E0EB", "#EAEAF2",
            "#E0E6EF", "#D8DFEA", "#E6EDF5", "#DEE8F6",
        ],
        useGradients: true,
        useBorderGradients: false,
        gradientStrength: 0.09);

    public static Theme Dracula => CreatePreset(
        backgroundColor: "#282A36",
        foregroundColor: "#F8F8F2",
        accentColor: "#BD93F9",
        mutedColor: "#B2B6C8",
        surfaceColor: "#343746",
        borderColor: "#6272A4",
        lineColor: "#FF79C6",
        nodePalette:
        [
            "#343746", "#3D4061", "#4B3F72", "#3C5668",
            "#4E546A", "#5B4468", "#42566B", "#3C404A",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.11,
        useMultiStopBorderGradient: true);

    public static Theme TokyoNight => CreatePreset(
        backgroundColor: "#1A1B26",
        foregroundColor: "#C0CAF5",
        accentColor: "#7AA2F7",
        mutedColor: "#8F9CC6",
        surfaceColor: "#24283B",
        borderColor: "#3D59A1",
        lineColor: "#7DCFFF",
        nodePalette:
        [
            "#24283B", "#2A3151", "#283457", "#263C54",
            "#3B2F56", "#214969", "#2E3B5B", "#263245",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.11,
        useMultiStopBorderGradient: true);

    public static Theme TokyoNightStorm => CreatePreset(
        backgroundColor: "#24283B",
        foregroundColor: "#A9B1D6",
        accentColor: "#7AA2F7",
        mutedColor: "#565F89",
        surfaceColor: "#2A3151",
        borderColor: "#3D59A1",
        lineColor: "#3D59A1",
        nodePalette:
        [
            "#2A3151", "#2B3658", "#2A3C54", "#3B2F56",
            "#214969", "#2E3B5B", "#263245", "#283457",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.11,
        useMultiStopBorderGradient: true);

    public static Theme TokyoNightLight => CreatePreset(
        backgroundColor: "#D5D6DB",
        foregroundColor: "#343B58",
        accentColor: "#34548A",
        mutedColor: "#9699A3",
        surfaceColor: "#E1E4EA",
        borderColor: "#A7AFBF",
        lineColor: "#34548A",
        nodePalette:
        [
            "#E6E8EE", "#DFE5EE", "#E9E5F0", "#E2E8F1",
            "#EDE7E2", "#E6E0F0", "#E5E7EC", "#DDE4EF",
        ],
        useGradients: true,
        useBorderGradients: false,
        gradientStrength: 0.10);

    public static Theme CatppuccinLatte => CreatePreset(
        backgroundColor: "#EFF1F5",
        foregroundColor: "#4C4F69",
        accentColor: "#8839EF",
        mutedColor: "#8C8FA1",
        surfaceColor: "#E6E9EF",
        borderColor: "#7287FD",
        lineColor: "#7C7F93",
        nodePalette:
        [
            "#E6E9EF", "#DDE6F7", "#F2E4FF", "#E4F4EF",
            "#FFE7D6", "#E7E3FF", "#F5E3EC", "#E4EFF8",
        ],
        useGradients: true,
        useBorderGradients: false,
        gradientStrength: 0.10);

    public static Theme CatppuccinMocha => CreatePreset(
        backgroundColor: "#1E1E2E",
        foregroundColor: "#CDD6F4",
        accentColor: "#CBA6F7",
        mutedColor: "#A6ADC8",
        surfaceColor: "#313244",
        borderColor: "#89B4FA",
        lineColor: "#74C7EC",
        nodePalette:
        [
            "#313244", "#3B3552", "#394B65", "#365E66",
            "#5A446B", "#47505F", "#3B4058", "#364156",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.10);

    public static Theme SolarizedLight => CreatePreset(
        backgroundColor: "#FDF6E3",
        foregroundColor: "#657B83",
        accentColor: "#268BD2",
        mutedColor: "#93A1A1",
        surfaceColor: "#F7EFD9",
        borderColor: "#93A1A1",
        lineColor: "#839496",
        nodePalette:
        [
            "#F7EFD9", "#EFF3D2", "#E8F1E6", "#E8ECF4",
            "#F6E8D8", "#EEE6F7", "#EFE2D2", "#E6EEE6",
        ],
        useGradients: true,
        useBorderGradients: false,
        gradientStrength: 0.09);

    public static Theme SolarizedDark => CreatePreset(
        backgroundColor: "#002B36",
        foregroundColor: "#93A1A1",
        accentColor: "#268BD2",
        mutedColor: "#657B83",
        surfaceColor: "#073642",
        borderColor: "#586E75",
        lineColor: "#839496",
        nodePalette:
        [
            "#073642", "#0A4252", "#124A57", "#27464E",
            "#2D4650", "#18424A", "#114451", "#0D3947",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.09);

    public static Theme OneDark => CreatePreset(
        backgroundColor: "#282C34",
        foregroundColor: "#ABB2BF",
        accentColor: "#61AFEF",
        mutedColor: "#8C95A6",
        surfaceColor: "#2F3640",
        borderColor: "#4B5263",
        lineColor: "#7F848E",
        nodePalette:
        [
            "#2F3640", "#334154", "#2F4C63", "#3C4453",
            "#4A4058", "#34495E", "#3A414F", "#314450",
        ],
        useGradients: true,
        useBorderGradients: true,
        gradientStrength: 0.10);

    /// <summary>Dark cyberpunk theme with neon glow accents and bold multi-stop border gradients.</summary>
    public static Theme Cyberpunk => CreateCyberpunkTheme();

    /// <summary>Dark synthwave theme with warm sunset gradients and analog glow.</summary>
    public static Theme Synthwave => CreateSynthwaveTheme();

    /// <summary>Light frosted-glass theme with translucent sheen and specular highlights.</summary>
    public static Theme Glass => CreateGlassTheme();

    /// <summary>Clean neumorphic theme with paired light and dark soft shadows for tactile depth.</summary>
    public static Theme Neumorphic => CreateNeumorphicTheme();

    /// <summary>Dark neon theme with vivid glass-glow halos around nodes.</summary>
    public static Theme Neon => CreateNeonTheme();

    // ── Palette lookup ────────────────────────────────────────────────────────

    /// <summary>All built-in theme names supported by <see cref="GetByName(string?)"/>.</summary>
    public static IReadOnlyList<string> BuiltInThemeNames => BuiltInThemeNameArray;

    /// <summary>
    /// Returns a built-in <see cref="Theme"/> by name (case-insensitive), or <see langword="null"/>
    /// for an unrecognised name.
    /// </summary>
    /// <param name="name">A built-in theme name such as <c>default</c>, <c>dark</c>, or <c>tokyo-night</c>.</param>
    public static Theme? GetByName(string? name) =>
        name?.Trim().ToLowerInvariant() switch
        {
            "default" => Default,
            "zinc-light" => ZincLight,
            "zinc-dark" => ZincDark,
            "dark" => Dark,
            "neutral" => Neutral,
            "forest" => Forest,
            "presentation" => Presentation,
            "prism" => Prism,
            "angled-light" => AngledLight,
            "angled-dark" => AngledDark,
            "github-light" => GithubLight,
            "github-dark" => GithubDark,
            "nord" => Nord,
            "nord-light" => NordLight,
            "dracula" => Dracula,
            "tokyo-night" => TokyoNight,
            "tokyo-night-storm" => TokyoNightStorm,
            "tokyo-night-light" => TokyoNightLight,
            "catppuccin-latte" => CatppuccinLatte,
            "catppuccin-mocha" => CatppuccinMocha,
            "solarized-light" => SolarizedLight,
            "solarized-dark" => SolarizedDark,
            "one-dark" => OneDark,
            "cyberpunk" => Cyberpunk,
            "synthwave" => Synthwave,
            "glass" => Glass,
            "neumorphic" => Neumorphic,
            "neon" => Neon,
            _ => null,
        };

    /// <summary>
    /// Creates a <see cref="Theme"/> from a semantic color set.
    /// </summary>
    public static Theme FromColors(
        string backgroundColor,
        string foregroundColor,
        string? accentColor = null,
        string? mutedColor = null,
        string? surfaceColor = null,
        string? borderColor = null,
        string? lineColor = null,
        bool useGradients = false,
        bool useBorderGradients = false,
        double gradientStrength = 0.12)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundColor);
        ArgumentException.ThrowIfNullOrWhiteSpace(foregroundColor);

        bool isLightBackground = ColorUtils.IsLight(backgroundColor);
        string accent = accentColor ?? (isLightBackground ? "#2563EB" : "#60A5FA");
        string muted = mutedColor ?? ColorUtils.Blend(foregroundColor, backgroundColor, isLightBackground ? 0.42 : 0.36);
        string surface = surfaceColor ?? ColorUtils.Blend(backgroundColor, accent, isLightBackground ? 0.10 : 0.18);
        string border = borderColor ?? ColorUtils.Blend(accent, backgroundColor, isLightBackground ? 0.22 : 0.12);
        string line = lineColor ?? ColorUtils.Blend(foregroundColor, backgroundColor, isLightBackground ? 0.36 : 0.30);

        var palette = new List<string>
        {
            ColorUtils.Blend(surface, accent, 0.05),
            ColorUtils.Blend(surface, accent, 0.12),
            ColorUtils.Blend(surface, accent, 0.18),
            ColorUtils.Blend(surface, foregroundColor, isLightBackground ? 0.04 : 0.08),
            ColorUtils.Blend(surface, accent, 0.24),
            ColorUtils.Blend(surface, foregroundColor, isLightBackground ? 0.08 : 0.12),
        };

        return new Theme
        {
            PrimaryColor = accent,
            SecondaryColor = ColorUtils.Blend(accent, backgroundColor, 0.35),
            AccentColor = ColorUtils.Blend(accent, foregroundColor, 0.10),
            BackgroundColor = backgroundColor,
            SurfaceColor = surface,
            BorderColor = border,
            NodeFillColor = surface,
            NodeStrokeColor = border,
            GroupFillColor = ColorUtils.WithOpacity(ColorUtils.Blend(backgroundColor, surface, isLightBackground ? 0.82 : 0.74), isLightBackground ? 0.92 : 0.88),
            GroupStrokeColor = ColorUtils.Blend(border, backgroundColor, isLightBackground ? 0.10 : 0.06),
            EdgeColor = line,
            TextColor = foregroundColor,
            TitleTextColor = foregroundColor,
            SubtleTextColor = muted,
            NodePalette = palette,
            NodeStrokePalette = CreateStrokePalette(palette, isLightBackground ? 0.26 : 0.18),
            BorderGradientStops = null,
            UseGradients = useGradients,
            UseBorderGradients = useBorderGradients,
            GradientStrength = NormalizeGradientStrength(gradientStrength),
        };
    }

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

        string bg = backgroundColor ?? "#FFFFFF";
        string primary = primaryColor;
        string secondary = secondaryColor ?? ColorUtils.Lighten(primary, 0.20);
        string accent = accentColor ?? ColorUtils.Darken(primary, 0.12);
        bool isLightBackground = ColorUtils.IsLight(bg);

        var palette = new List<string>
        {
            ColorUtils.Blend(bg, primary, isLightBackground ? 0.18 : 0.24),
            ColorUtils.Blend(bg, secondary, isLightBackground ? 0.18 : 0.24),
            ColorUtils.Blend(bg, accent, isLightBackground ? 0.18 : 0.24),
            ColorUtils.Blend(bg, primary, isLightBackground ? 0.28 : 0.34),
            ColorUtils.Blend(bg, secondary, isLightBackground ? 0.28 : 0.34),
            ColorUtils.Blend(bg, accent, isLightBackground ? 0.28 : 0.34),
            ColorUtils.Blend(bg, primary, isLightBackground ? 0.10 : 0.16),
            ColorUtils.Blend(bg, secondary, isLightBackground ? 0.10 : 0.16),
        };

        var theme = FromColors(
            backgroundColor: bg,
            foregroundColor: ColorUtils.ChooseTextColor(bg, lightTextHex: "#E2E8F0", darkTextHex: "#0F172A"),
            accentColor: primary,
            mutedColor: ColorUtils.Blend(ColorUtils.ChooseTextColor(bg, lightTextHex: "#E2E8F0", darkTextHex: "#0F172A"), bg, isLightBackground ? 0.42 : 0.34),
            surfaceColor: ColorUtils.Blend(bg, primary, isLightBackground ? 0.16 : 0.22),
            borderColor: ColorUtils.Blend(primary, bg, isLightBackground ? 0.18 : 0.10),
            lineColor: ColorUtils.Blend(ColorUtils.ChooseTextColor(bg, lightTextHex: "#E2E8F0", darkTextHex: "#0F172A"), bg, isLightBackground ? 0.48 : 0.34),
            useGradients: true,
            useBorderGradients: false,
            gradientStrength: 0.12);

        theme.PrimaryColor = primary;
        theme.SecondaryColor = secondary;
        theme.AccentColor = accent;
        theme.NodePalette = palette;
        theme.NodeStrokePalette = CreateStrokePalette(palette, isLightBackground ? 0.26 : 0.18);
        return theme;
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

    /// <summary>
    /// Optional explicit stroke gradient stops used when <see cref="UseBorderGradients"/> is enabled.
    /// When omitted, the renderer derives a simpler two-stop border gradient from the stroke color.
    /// </summary>
    public List<string>? BorderGradientStops { get; set; }

    // ── Color properties ──────────────────────────────────────────────────────

    public string PrimaryColor { get; set; } = "#4F81BD";
    public string SecondaryColor { get; set; } = "#70AD47";
    public string AccentColor { get; set; } = "#ED7D31";
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string SurfaceColor { get; set; } = "#F6F8FB";
    public string BorderColor { get; set; } = "#6B7A90";
    public string NodeFillColor { get; set; } = "#DAE8FC";
    public string NodeStrokeColor { get; set; } = "#4F81BD";
    public string GroupFillColor { get; set; } = "#F3F4F6";
    public string GroupStrokeColor { get; set; } = "#D1D5DB";
    public string EdgeColor { get; set; } = "#555555";
    public string TextColor { get; set; } = "#1F2937";
    public string TitleTextColor { get; set; } = "#0F172A";
    public string SubtleTextColor { get; set; } = "#6B7280";
    public string? FillStyle { get; set; }
    public string? ShadowStyle { get; set; }
    public bool UseGradients { get; set; }
    public bool UseBorderGradients { get; set; }
    public double GradientStrength { get; set; } = 0.12;
    public bool TransparentBackground { get; set; }
    public bool UseNodeShadows { get; set; }
    public string ShadowColor { get; set; } = "#0F172A";
    public double ShadowOpacity { get; set; } = 0.14;
    public double ShadowBlur { get; set; } = 1.50;
    public double ShadowOffsetX { get; set; }
    public double ShadowOffsetY { get; set; } = 1.40;

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

    private static Theme CreatePreset(
        string backgroundColor,
        string foregroundColor,
        string accentColor,
        string? mutedColor,
        string? surfaceColor,
        string? borderColor,
        string? lineColor,
        IEnumerable<string> nodePalette,
        bool useGradients,
        bool useBorderGradients,
        double gradientStrength,
        bool useMultiStopBorderGradient = false)
    {
        var theme = FromColors(
            backgroundColor: backgroundColor,
            foregroundColor: foregroundColor,
            accentColor: accentColor,
            mutedColor: mutedColor,
            surfaceColor: surfaceColor,
            borderColor: borderColor,
            lineColor: lineColor,
            useGradients: useGradients,
            useBorderGradients: useBorderGradients,
            gradientStrength: gradientStrength);

        theme.NodePalette = [.. nodePalette];
        theme.NodeStrokePalette = CreateStrokePalette(theme.NodePalette, ColorUtils.IsLight(backgroundColor) ? 0.24 : 0.18);

        if (useMultiStopBorderGradient)
            theme.BorderGradientStops = CreateExpressiveBorderStops(theme.NodePalette, backgroundColor);

        return theme;
    }

    private static Theme CreatePresentationTheme()
    {
        var theme = CreatePreset(
            backgroundColor: "#FFFDF8",
            foregroundColor: "#111827",
            accentColor: "#0F52BA",
            mutedColor: "#4B5563",
            surfaceColor: "#E7F0FF",
            borderColor: "#0F52BA",
            lineColor: "#173F6D",
            nodePalette:
            [
                "#E7F0FF", "#FFE6D8", "#FFF0C7", "#DCF5E8",
                "#EFE3FF", "#DFF2FF", "#FEE2E2", "#E8F5E9",
            ],
            useGradients: true,
            useBorderGradients: true,
            gradientStrength: 0.12,
            useMultiStopBorderGradient: true);

        theme.FontSize = 16;
        theme.TitleFontSize = 20;
        theme.NodePadding = 16;
        theme.DiagramPadding = 32;
        theme.BorderRadius = 12;
        theme.StrokeWidth = 1.8;
        theme.ShadowStyle = "soft";
        theme.UseNodeShadows = true;
        theme.ShadowColor = "#0F172A";
        theme.ShadowOpacity = 0.16;
        theme.ShadowBlur = 1.65;
        theme.ShadowOffsetY = 1.55;
        return theme;
    }

    private static Theme CreatePrismTheme()
    {
        var theme = CreatePreset(
            backgroundColor: "#FFFFFF",
            foregroundColor: "#111827",
            accentColor: "#2563EB",
            mutedColor: "#64748B",
            surfaceColor: "#FFFFFF",
            borderColor: "#CBD5E1",
            lineColor: "#64748B",
            nodePalette:
            [
                "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF",
                "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF",
            ],
            useGradients: false,
            useBorderGradients: true,
            gradientStrength: 0.10,
            useMultiStopBorderGradient: false);

        theme.NodeFillColor = "#FFFFFF";
        theme.NodeStrokeColor = "#CBD5E1";
        theme.GroupFillColor = "#FFFFFFE6";
        theme.GroupStrokeColor = "#D7DEE8";
        theme.BorderGradientStops =
        [
            "#2563EB",
            "#7C3AED",
            "#DB2777",
            "#F59E0B",
        ];
        theme.BorderRadius = 10;
        return theme;
    }

    private static Theme CreateAngledLightTheme()
    {
        var theme = CreatePreset(
            backgroundColor: "#FFFDF8",
            foregroundColor: "#111827",
            accentColor: "#2563EB",
            mutedColor: "#64748B",
            surfaceColor: "#F7FAFF",
            borderColor: "#C8D3E2",
            lineColor: "#64748B",
            nodePalette:
            [
                "#EEF4FF", "#ECF8F1", "#FFF3EA", "#F4ECFF",
                "#EAF8F8", "#FFF1F5", "#EFF3FA", "#F7F3EB",
            ],
            useGradients: true,
            useBorderGradients: false,
            gradientStrength: 0.16,
            useMultiStopBorderGradient: false);

        theme.NodeFillColor = "#F7FAFF";
        theme.NodeStrokeColor = "#C8D3E2";
        theme.GroupFillColor = "#FFFFFFE6";
        theme.GroupStrokeColor = "#DAE1EA";
        theme.ShadowStyle = "soft";
        theme.UseNodeShadows = true;
        theme.ShadowColor = "#0F172A";
        theme.ShadowOpacity = 0.16;
        theme.ShadowBlur = 1.65;
        theme.ShadowOffsetY = 1.55;
        theme.BorderRadius = 10;
        return theme;
    }

    private static Theme CreateAngledDarkTheme()
    {
        var theme = CreatePreset(
            backgroundColor: "#0F172A",
            foregroundColor: "#E2E8F0",
            accentColor: "#60A5FA",
            mutedColor: "#94A3B8",
            surfaceColor: "#1A2438",
            borderColor: "#334155",
            lineColor: "#8FA3B8",
            nodePalette:
            [
                "#21314D", "#1C3A47", "#3A2F5A", "#2B435A",
                "#3B3046", "#21363F", "#263851", "#1E3342",
            ],
            useGradients: true,
            useBorderGradients: false,
            gradientStrength: 0.16,
            useMultiStopBorderGradient: false);

        theme.NodeFillColor = "#1A2438";
        theme.NodeStrokeColor = "#334155";
        theme.GroupFillColor = "#1A2438E0";
        theme.GroupStrokeColor = "#3A475C";
        theme.TitleTextColor = "#F8FAFC";
        theme.BorderRadius = 10;
        return theme;
    }

    private static Theme CreateCyberpunkTheme()
    {
        var theme = CreatePreset(
            backgroundColor: "#0A0A1A",
            foregroundColor: "#E0E0F0",
            accentColor: "#FF2D95",
            mutedColor: "#7A7A9E",
            surfaceColor: "#12122A",
            borderColor: "#FF2D95",
            lineColor: "#00F0FF",
            nodePalette:
            [
                "#120E2A", "#14102E", "#160E30", "#131028",
                "#18103A", "#140E2C", "#1A1038", "#151030",
            ],
            useGradients: true,
            useBorderGradients: true,
            gradientStrength: 0.18,
            useMultiStopBorderGradient: false);

        theme.NodeFillColor = "#12122A";
        theme.NodeStrokeColor = "#FF2D95";
        theme.GroupFillColor = "#12122AE0";
        theme.GroupStrokeColor = "#8B5CF6";
        theme.TitleTextColor = "#00F0FF";
        theme.EdgeColor = "#00F0FF";
        theme.BorderGradientStops =
        [
            "#00F0FF",   // cyan neon
            "#8B5CF6",   // purple
            "#FF2D95",   // hot pink
            "#39FF14",   // neon green
        ];
        theme.BorderRadius = 6;
        theme.StrokeWidth = 1.8;
        theme.ShadowStyle = "glow";
        theme.UseNodeShadows = true;
        theme.ShadowColor = "#FF2D95";
        theme.ShadowOpacity = 0.55;
        theme.ShadowBlur = 4.0;
        theme.ShadowOffsetX = 0;
        theme.ShadowOffsetY = 0;
        return theme;
    }

    private static Theme CreateSynthwaveTheme()
    {
        var theme = CreatePreset(
            backgroundColor: "#1A0030",
            foregroundColor: "#F0E0FF",
            accentColor: "#FF6EC7",
            mutedColor: "#9080A8",
            surfaceColor: "#2A1040",
            borderColor: "#FF6EC7",
            lineColor: "#FFB347",
            nodePalette:
            [
                "#2A1040", "#2E1248", "#321450", "#261042",
                "#301252", "#2C1046", "#34144E", "#281044",
            ],
            useGradients: true,
            useBorderGradients: true,
            gradientStrength: 0.16,
            useMultiStopBorderGradient: false);

        theme.NodeFillColor = "#2A1040";
        theme.NodeStrokeColor = "#FF6EC7";
        theme.GroupFillColor = "#2A1040E0";
        theme.GroupStrokeColor = "#B24BF3";
        theme.TitleTextColor = "#FFB347";
        theme.EdgeColor = "#FFB347";
        theme.BorderGradientStops =
        [
            "#FF6EC7",   // hot pink
            "#B24BF3",   // violet
            "#FFB347",   // sunset orange
            "#FF3864",   // neon red-pink
        ];
        theme.BorderRadius = 6;
        theme.StrokeWidth = 1.8;
        theme.ShadowStyle = "glow";
        theme.UseNodeShadows = true;
        theme.ShadowColor = "#FF6EC7";
        theme.ShadowOpacity = 0.45;
        theme.ShadowBlur = 5.0;
        theme.ShadowOffsetX = 0;
        theme.ShadowOffsetY = 0;
        return theme;
    }

    private static Theme CreateGlassTheme()
    {
        var theme = CreatePreset(
            backgroundColor: "#E2E8F0",
            foregroundColor: "#1A2B42",
            accentColor: "#2563EB",
            mutedColor: "#64748B",
            surfaceColor: "#F8FAFC",
            borderColor: "#94A3B8",
            lineColor: "#475569",
            nodePalette:
            [
                "#C7D8EED9", "#C7E8D5D9", "#E8D5C7D9", "#D5C7E8D9",
                "#C7E8E2D9", "#E8C7D2D9", "#D5DFEAD9", "#E8E2C7D9",
            ],
            useGradients: true,
            useBorderGradients: false,
            gradientStrength: 0.08);

        theme.NodeFillColor = "#C7D8EED9";
        theme.NodeStrokeColor = "#94A3B8";
        theme.GroupFillColor = "#CBD5E1B3";
        theme.GroupStrokeColor = "#94A3B8";
        theme.BorderRadius = 14;
        theme.StrokeWidth = 1.4;
        theme.ShadowStyle = "frosted-glass";
        theme.UseNodeShadows = true;
        theme.ShadowColor = "#0F172A";
        theme.ShadowOpacity = 0.18;
        theme.ShadowBlur = 3.50;
        theme.ShadowOffsetY = 3.00;
        return theme;
    }

    private static Theme CreateNeumorphicTheme()
    {
        var theme = CreatePreset(
            backgroundColor: "#E4E9F0",
            foregroundColor: "#2D3748",
            accentColor: "#4299E1",
            mutedColor: "#8896A6",
            surfaceColor: "#E4E9F0",
            borderColor: "#D2D8E0",
            lineColor: "#8896A6",
            nodePalette:
            [
                "#E4E9F0", "#E4E9F0", "#E4E9F0", "#E4E9F0",
                "#E4E9F0", "#E4E9F0", "#E4E9F0", "#E4E9F0",
            ],
            useGradients: false,
            useBorderGradients: false,
            gradientStrength: 0.0);

        theme.NodeFillColor = "#E4E9F0";
        theme.NodeStrokeColor = "#D2D8E0";
        theme.GroupFillColor = "#E4E9F0";
        theme.GroupStrokeColor = "#D2D8E0";
        theme.BorderRadius = 16;
        theme.StrokeWidth = 0.8;
        theme.ShadowStyle = "neumorphic";
        theme.UseNodeShadows = true;
        theme.ShadowColor = "#8B98AC";
        theme.ShadowOpacity = 0.55;
        theme.ShadowBlur = 4.00;
        theme.ShadowOffsetX = 0;
        theme.ShadowOffsetY = 0;
        return theme;
    }

    private static Theme CreateNeonTheme()
    {
        var theme = CreatePreset(
            backgroundColor: "#0D0D1A",
            foregroundColor: "#E0E0F0",
            accentColor: "#00FF88",
            mutedColor: "#7A7A9E",
            surfaceColor: "#141428",
            borderColor: "#00FF88",
            lineColor: "#00CCFF",
            nodePalette:
            [
                "#101028", "#121230", "#0E1828", "#141432",
                "#161636", "#0E1E30", "#121228", "#101830",
            ],
            useGradients: true,
            useBorderGradients: true,
            gradientStrength: 0.14);

        theme.NodeFillColor = "#101028";
        theme.NodeStrokeColor = "#00FF88";
        theme.GroupFillColor = "#141428E0";
        theme.GroupStrokeColor = "#00CCFF";
        theme.TitleTextColor = "#00FF88";
        theme.EdgeColor = "#00CCFF";
        theme.BorderGradientStops =
        [
            "#00FF88",   // neon green
            "#00CCFF",   // electric blue
            "#FF00FF",   // magenta
            "#FFD700",   // gold
        ];
        theme.BorderRadius = 8;
        theme.StrokeWidth = 1.6;
        theme.ShadowStyle = "glass-glow";
        theme.UseNodeShadows = true;
        theme.ShadowBlur = 4.00;
        theme.ShadowOffsetX = 0;
        theme.ShadowOffsetY = 0;
        return theme;
    }

    private static List<string> CreateStrokePalette(IEnumerable<string> palette, double darkenAmount) =>
        [.. palette.Select(color => ColorUtils.Darken(color, darkenAmount))];

    public static List<string> CreateExpressiveBorderStops(IEnumerable<string>? palette, string backgroundColor)
    {
        bool isLightBackground = ColorUtils.IsLight(backgroundColor);
        var source = (palette ?? []).Where(color => !string.IsNullOrWhiteSpace(color)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (source.Count == 0)
            source = isLightBackground
                ? ["#2563EB", "#7C3AED", "#DB2777", "#F97316"]
                : ["#38BDF8", "#818CF8", "#C084FC", "#FB7185"];

        int[] indices =
        [
            0,
            Math.Min(source.Count - 1, Math.Max(0, (int)Math.Round((source.Count - 1) * 0.33))),
            Math.Min(source.Count - 1, Math.Max(0, (int)Math.Round((source.Count - 1) * 0.66))),
            source.Count - 1,
        ];

        return
        [
            .. indices
                .Distinct()
                .Select(index => source[index])
                .Select(color => isLightBackground
                    ? ColorUtils.Darken(color, 0.18)
                    : ColorUtils.Lighten(color, 0.10))
                .Select(color => ColorUtils.Blend(color, backgroundColor, isLightBackground ? 0.04 : 0.12))
        ];
    }

    private static double NormalizeGradientStrength(double gradientStrength) => Math.Clamp(gradientStrength, 0, 0.45);
}
