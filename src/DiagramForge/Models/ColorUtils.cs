namespace DiagramForge.Models;

/// <summary>
/// Utility methods for basic hex-color manipulation used by <see cref="Theme"/> and the renderer.
/// </summary>
public static class ColorUtils
{
    /// <summary>
    /// Returns a lighter version of the given hex color by blending toward white.
    /// </summary>
    /// <param name="hex">Hex color string, e.g. <c>#4F81BD</c>.</param>
    /// <param name="amount">Blend factor 0–1 (0 = unchanged, 1 = white).</param>
    public static string Lighten(string hex, double amount)
    {
        (int r, int g, int b) = ParseHex(hex);
        int lr = Clamp((int)(r + (255 - r) * amount));
        int lg = Clamp((int)(g + (255 - g) * amount));
        int lb = Clamp((int)(b + (255 - b) * amount));
        return ToHex(lr, lg, lb);
    }

    /// <summary>
    /// Returns a darker version of the given hex color by blending toward black.
    /// </summary>
    /// <param name="hex">Hex color string, e.g. <c>#4F81BD</c>.</param>
    /// <param name="amount">Blend factor 0–1 (0 = unchanged, 1 = black).</param>
    public static string Darken(string hex, double amount)
    {
        (int r, int g, int b) = ParseHex(hex);
        int dr = Clamp((int)(r * (1 - amount)));
        int dg = Clamp((int)(g * (1 - amount)));
        int db = Clamp((int)(b * (1 - amount)));
        return ToHex(dr, dg, db);
    }

    /// <summary>
    /// Returns a desaturated (muted) version of the given hex color.
    /// </summary>
    /// <param name="hex">Hex color string, e.g. <c>#4F81BD</c>.</param>
    /// <param name="amount">Desaturation factor 0–1 (0 = unchanged, 1 = full grayscale).</param>
    public static string Desaturate(string hex, double amount)
    {
        (int r, int g, int b) = ParseHex(hex);
        // Perceived luminance weights (BT.601)
        double luminance = r * 0.299 + g * 0.587 + b * 0.114;
        int dr = Clamp((int)(r + (luminance - r) * amount));
        int dg = Clamp((int)(g + (luminance - g) * amount));
        int db = Clamp((int)(b + (luminance - b) * amount));
        return ToHex(dr, dg, db);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    public static (int r, int g, int b) ParseHex(string hex)
    {
        string cleaned = hex.TrimStart('#');

        // Expand 3-char shorthand to 6 chars
        if (cleaned.Length == 3)
            cleaned = string.Concat(cleaned[0], cleaned[0], cleaned[1], cleaned[1], cleaned[2], cleaned[2]);

        if (cleaned.Length != 6)
            throw new ArgumentException($"Invalid hex color: '{hex}'", nameof(hex));

        int r = Convert.ToInt32(cleaned[..2], 16);
        int g = Convert.ToInt32(cleaned[2..4], 16);
        int b = Convert.ToInt32(cleaned[4..6], 16);
        return (r, g, b);
    }

    private static int Clamp(int value) => Math.Max(0, Math.Min(255, value));

    private static string ToHex(int r, int g, int b) =>
        $"#{r:X2}{g:X2}{b:X2}";
}
