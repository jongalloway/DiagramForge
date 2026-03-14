namespace DiagramForge.Models;

/// <summary>
/// Utility methods for basic hex-color manipulation used by <see cref="Theme"/> and the renderer.
/// </summary>
/// <remarks>
/// Supported hex formats:
/// <list type="bullet">
///   <item><c>#RGB</c> — 3-digit shorthand (e.g. <c>#F80</c>)</item>
///   <item><c>#RGBA</c> — 4-digit shorthand with alpha (e.g. <c>#F80C</c>)</item>
///   <item><c>#RRGGBB</c> — 6-digit full (e.g. <c>#FF8800</c>)</item>
///   <item><c>#RRGGBBAA</c> — 8-digit full with alpha (e.g. <c>#FF8800CC</c>)</item>
/// </list>
/// </remarks>
public static class ColorUtils
{
    /// <summary>
    /// Blends <paramref name="fromHex"/> toward <paramref name="toHex"/> by <paramref name="amount"/>.
    /// </summary>
    /// <param name="fromHex">Starting hex color string.</param>
    /// <param name="toHex">Target hex color string.</param>
    /// <param name="amount">Blend factor 0–1 (0 = <paramref name="fromHex"/>, 1 = <paramref name="toHex"/>).</param>
    public static string Blend(string fromHex, string toHex, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        var (fromR, fromG, fromB, fromA) = ParseHexWithAlpha(fromHex);
        var (toR, toG, toB, toA) = ParseHexWithAlpha(toHex);

        return ToHex(
            Clamp((int)Math.Round(fromR + (toR - fromR) * amount)),
            Clamp((int)Math.Round(fromG + (toG - fromG) * amount)),
            Clamp((int)Math.Round(fromB + (toB - fromB) * amount)),
            Clamp((int)Math.Round(fromA + (toA - fromA) * amount)));
    }

    /// <summary>
    /// Returns a lighter version of the given hex color by blending toward white.
    /// The alpha channel (if present) is preserved in the output.
    /// </summary>
    /// <param name="hex">Hex color string.</param>
    /// <param name="amount">Blend factor 0–1 (0 = unchanged, 1 = white).</param>
    public static string Lighten(string hex, double amount)
    {
        var (r, g, b, a) = ParseHexWithAlpha(hex);
        return ToHex(
            Clamp((int)(r + (255 - r) * amount)),
            Clamp((int)(g + (255 - g) * amount)),
            Clamp((int)(b + (255 - b) * amount)),
            a);
    }

    /// <summary>
    /// Returns a darker version of the given hex color by blending toward black.
    /// The alpha channel (if present) is preserved in the output.
    /// </summary>
    /// <param name="hex">Hex color string.</param>
    /// <param name="amount">Blend factor 0–1 (0 = unchanged, 1 = black).</param>
    public static string Darken(string hex, double amount)
    {
        var (r, g, b, a) = ParseHexWithAlpha(hex);
        return ToHex(
            Clamp((int)(r * (1 - amount))),
            Clamp((int)(g * (1 - amount))),
            Clamp((int)(b * (1 - amount))),
            a);
    }

    /// <summary>
    /// Returns a desaturated (muted) version of the given hex color.
    /// The alpha channel (if present) is preserved in the output.
    /// </summary>
    /// <param name="hex">Hex color string.</param>
    /// <param name="amount">Desaturation factor 0–1 (0 = unchanged, 1 = full grayscale).</param>
    public static string Desaturate(string hex, double amount)
    {
        var (r, g, b, a) = ParseHexWithAlpha(hex);
        // Perceived luminance weights (BT.601)
        double luminance = r * 0.299 + g * 0.587 + b * 0.114;
        return ToHex(
            Clamp((int)(r + (luminance - r) * amount)),
            Clamp((int)(g + (luminance - g) * amount)),
            Clamp((int)(b + (luminance - b) * amount)),
            a);
    }

    /// <summary>
    /// Returns the same color with the requested alpha opacity.
    /// </summary>
    public static string WithOpacity(string hex, double opacity)
    {
        var (r, g, b, _) = ParseHexWithAlpha(hex);
        int a = Clamp((int)Math.Round(Math.Clamp(opacity, 0, 1) * 255));
        return ToHex(r, g, b, a);
    }

    /// <summary>
    /// Computes a simple perceived luminance value for a hex color.
    /// </summary>
    public static double GetLuminance(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return r * 0.299 + g * 0.587 + b * 0.114;
    }

    /// <summary>
    /// Returns <see langword="true"/> when a color is perceived as light.
    /// </summary>
    public static bool IsLight(string hex) => GetLuminance(hex) > 128;

    /// <summary>
    /// Picks a readable text color for a given background.
    /// </summary>
    public static string ChooseTextColor(string backgroundHex, string lightTextHex = "#F8FAFC", string darkTextHex = "#0F172A") =>
        IsLight(backgroundHex) ? darkTextHex : lightTextHex;

    /// <summary>
    /// Computes the WCAG 2.1 relative luminance of a hex color.
    /// Uses sRGB gamma correction and BT.709 coefficients.
    /// Returns a value between 0 (black) and 1 (white).
    /// </summary>
    /// <remarks>
    /// See <see href="https://www.w3.org/TR/WCAG21/#dfn-relative-luminance">WCAG 2.1 § 1.4.3</see>.
    /// </remarks>
    public static double GetRelativeLuminance(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return 0.2126 * LinearizeChannel(r / 255.0)
             + 0.7152 * LinearizeChannel(g / 255.0)
             + 0.0722 * LinearizeChannel(b / 255.0);
    }

    /// <summary>
    /// Computes the WCAG 2.1 contrast ratio between two hex colors.
    /// Returns a value between 1 (identical) and 21 (black on white).
    /// </summary>
    /// <remarks>
    /// WCAG AA thresholds: 4.5:1 for normal text, 3.0:1 for large text.
    /// See <see href="https://www.w3.org/TR/WCAG21/#contrast-minimum">WCAG 2.1 § 1.4.3</see>.
    /// </remarks>
    public static double GetContrastRatio(string hex1, string hex2)
    {
        double l1 = GetRelativeLuminance(hex1);
        double l2 = GetRelativeLuminance(hex2);
        double lighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Linearizes a single sRGB channel value for WCAG relative luminance calculation (IEC 61966-2-1).</summary>
    private static double LinearizeChannel(double value) =>
        value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);

    // ── Parsing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a hex color string and returns the RGB channels (alpha is discarded).
    /// Supports <c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>, and <c>#RRGGBBAA</c> formats.
    /// </summary>
    /// <exception cref="ArgumentException">The string is not a valid hex color.</exception>
    public static (int r, int g, int b) ParseHex(string hex)
    {
        var (r, g, b, _) = ParseHexWithAlpha(hex);
        return (r, g, b);
    }

    /// <summary>
    /// Parses a hex color string and returns the RGBA channels.
    /// Supports <c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>, and <c>#RRGGBBAA</c> formats.
    /// Colors without an explicit alpha channel return <c>a = 255</c> (fully opaque).
    /// </summary>
    /// <exception cref="ArgumentException">The string is not a valid hex color.</exception>
    public static (int r, int g, int b, int a) ParseHexWithAlpha(string hex)
    {
        string cleaned = ExpandShorthand(hex);

        if (!cleaned.All(Uri.IsHexDigit))
            throw new ArgumentException($"Invalid hex color: '{hex}'", nameof(hex));

        int r = Convert.ToInt32(cleaned[..2], 16);
        int g = Convert.ToInt32(cleaned[2..4], 16);
        int b = Convert.ToInt32(cleaned[4..6], 16);
        int a = cleaned.Length == 8 ? Convert.ToInt32(cleaned[6..8], 16) : 255;
        return (r, g, b, a);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Strips the leading <c>#</c> (required) and expands shorthand to canonical length (6 or 8 digits).
    /// </summary>
    /// <exception cref="ArgumentException">The string does not start with <c>#</c> or has an unsupported length.</exception>
    private static string ExpandShorthand(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex[0] != '#')
            throw new ArgumentException($"Invalid hex color: '{hex}' (must start with '#')", nameof(hex));

        string cleaned = hex[1..];
        return cleaned.Length switch
        {
            // #RGB  →  #RRGGBB
            3 => string.Concat(cleaned[0], cleaned[0], cleaned[1], cleaned[1], cleaned[2], cleaned[2]),
            // #RGBA →  #RRGGBBAA
            4 => string.Concat(cleaned[0], cleaned[0], cleaned[1], cleaned[1], cleaned[2], cleaned[2], cleaned[3], cleaned[3]),
            6 or 8 => cleaned,
            _ => throw new ArgumentException($"Invalid hex color: '{hex}'", nameof(hex)),
        };
    }

    private static int Clamp(int value) => Math.Max(0, Math.Min(255, value));

    /// <summary>
    /// Formats RGB(A) channels back to a hex color string.
    /// Omits the alpha byte when <paramref name="a"/> is 255 (fully opaque).
    /// </summary>
    private static string ToHex(int r, int g, int b, int a = 255) =>
        a == 255
            ? $"#{r:X2}{g:X2}{b:X2}"
            : $"#{r:X2}{g:X2}{b:X2}{a:X2}";
}

