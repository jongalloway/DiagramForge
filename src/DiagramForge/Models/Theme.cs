namespace DiagramForge.Models;

/// <summary>
/// Defines the visual theme (colors, fonts, sizing) applied to a rendered diagram.
/// </summary>
public class Theme
{
    public static Theme Default { get; } = new Theme();

    // ── Palette ──────────────────────────────────────────────────────────────
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
}
