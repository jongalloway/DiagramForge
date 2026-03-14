using System.Text;
using DiagramForge.Models;

namespace DiagramForge.Rendering;

internal static class SvgRenderSupport
{
    private const double AvgGlyphAdvanceEm = 0.6;

    internal static void AppendDefs(StringBuilder sb, Theme theme)
    {
        sb.AppendLine("  <defs>");

        // Standard filled triangle (used for Arrow / association)
        sb.AppendLine($"""    <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">""");
        sb.AppendLine($"""      <polygon points="0 0, 10 3.5, 0 7" fill="{Escape(theme.EdgeColor)}"/>""");
        sb.AppendLine("    </marker>");

        // Open (hollow) triangle — UML inheritance / realization (target end)
        sb.AppendLine($"""    <marker id="arrowhead-open" markerWidth="12" markerHeight="9" refX="11" refY="4.50" orient="auto">""");
        sb.AppendLine($"""      <polygon points="0 0, 12 4.50, 0 9" fill="{Escape(theme.BackgroundColor)}" stroke="{Escape(theme.EdgeColor)}" stroke-width="1.50"/>""");
        sb.AppendLine("    </marker>");

        // Filled diamond — UML composition (source end, use marker-start)
        sb.AppendLine($"""    <marker id="arrowhead-filled-diamond" markerWidth="14" markerHeight="9" refX="1" refY="4.50" orient="auto">""");
        sb.AppendLine($"""      <polygon points="1 4.50, 7 1, 13 4.50, 7 8" fill="{Escape(theme.EdgeColor)}"/>""");
        sb.AppendLine("    </marker>");

        // Open (hollow) diamond — UML aggregation (source end, use marker-start)
        sb.AppendLine($"""    <marker id="arrowhead-open-diamond" markerWidth="14" markerHeight="9" refX="1" refY="4.50" orient="auto">""");
        sb.AppendLine($"""      <polygon points="1 4.50, 7 1, 13 4.50, 7 8" fill="{Escape(theme.BackgroundColor)}" stroke="{Escape(theme.EdgeColor)}" stroke-width="1.50"/>""");
        sb.AppendLine("    </marker>");

        sb.AppendLine("  </defs>");
    }

    internal static string F(double v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    internal static double EstimateTextWidth(string? text, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return text
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n')
            .Max(line => line.Length) * fontSize * AvgGlyphAdvanceEm;
    }

    internal static bool TryResolveXyChartColors(Node node, Theme theme, out string fillColor, out string strokeColor)
    {
        fillColor = string.Empty;
        strokeColor = string.Empty;

        if (!node.Metadata.TryGetValue("xychart:kind", out var kindObj) || kindObj is not string kind)
            return false;

        if (!string.Equals(kind, "bar", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(kind, "linePoint", StringComparison.OrdinalIgnoreCase))
            return false;

        int paletteIndex = node.Metadata.TryGetValue("xychart:paletteIndex", out var paletteIndexObj)
            ? Convert.ToInt32(paletteIndexObj, System.Globalization.CultureInfo.InvariantCulture)
            : 0;

        fillColor = GetXyChartSeriesColor(theme, paletteIndex);
        strokeColor = GetXyChartSeriesStrokeColor(fillColor, theme);
        return true;
    }

    internal static string GetXyChartSeriesColor(Theme theme, int seriesIndex)
    {
        bool isLightBackground = ColorUtils.IsLight(theme.BackgroundColor);
        string accent = theme.AccentColor;

        string[] palette =
        [
            accent,
            isLightBackground ? ColorUtils.Darken(accent, 0.14) : ColorUtils.Lighten(accent, 0.14),
            isLightBackground ? ColorUtils.Darken(accent, 0.28) : ColorUtils.Lighten(accent, 0.28),
            ColorUtils.Blend(accent, theme.PrimaryColor, 0.32),
            ColorUtils.Blend(accent, theme.SecondaryColor, 0.28),
            isLightBackground ? ColorUtils.Blend(accent, theme.SurfaceColor, 0.18) : ColorUtils.Blend(accent, "#FFFFFF", 0.12),
            isLightBackground ? ColorUtils.Darken(accent, 0.40) : ColorUtils.Lighten(accent, 0.40),
            isLightBackground ? ColorUtils.Blend(accent, theme.BackgroundColor, 0.10) : ColorUtils.Blend(accent, theme.SurfaceColor, 0.22),
        ];

        return palette[Math.Abs(seriesIndex) % palette.Length];
    }

    internal static string GetXyChartSeriesStrokeColor(string fillColor, Theme theme) =>
        ColorUtils.IsLight(theme.BackgroundColor)
            ? ColorUtils.Darken(fillColor, 0.12)
            : ColorUtils.Lighten(fillColor, 0.10);

    internal static double GetXyChartBarRadius(Node node, Theme theme)
    {
        double maxRadius = Math.Min(Math.Min(node.Width, node.Height) * 0.18, Math.Max(4, theme.BorderRadius));
        return Math.Max(0, maxRadius);
    }

    internal static bool HasTextOnlyBackdrop(Node node, double? fillOpacity) =>
        !string.IsNullOrWhiteSpace(node.Label.Text)
        && (node.FillColor is not null || node.StrokeColor is not null || fillOpacity.HasValue);

    internal static string ResolveNodeTextColor(string fillColor, Theme theme)
    {
        string darkText = ColorUtils.IsLight(theme.TextColor) ? "#0F172A" : theme.TextColor;
        string lightText = ColorUtils.IsLight(theme.TextColor) ? theme.TextColor : "#F8FAFC";
        return ColorUtils.ChooseTextColor(fillColor, lightText, darkText);
    }

    internal static void AppendGradientDefs(
        StringBuilder sb,
        string indent,
        string prefix,
        string fillColor,
        string strokeColor,
        Theme theme,
        out string fillPaint,
        out string strokePaint)
    {
        fillPaint = Escape(fillColor);
        strokePaint = Escape(strokeColor);

        if (!theme.UseGradients && !theme.UseBorderGradients)
            return;

        sb.AppendLine($"{indent}<defs>");

        if (theme.UseGradients)
        {
            string fillId = prefix + "-fill-gradient";
            string fillStart = ColorUtils.Lighten(fillColor, Math.Max(theme.GradientStrength * 0.90, 0.06));
            string fillEnd = ColorUtils.Darken(fillColor, Math.Max(theme.GradientStrength * 0.60, 0.05));
            sb.AppendLine($"{indent}  <linearGradient id=\"{fillId}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"100%\">");
            sb.AppendLine($"{indent}    <stop offset=\"0%\" stop-color=\"{Escape(fillStart)}\"/>");
            sb.AppendLine($"{indent}    <stop offset=\"58%\" stop-color=\"{Escape(fillColor)}\"/>");
            sb.AppendLine($"{indent}    <stop offset=\"100%\" stop-color=\"{Escape(fillEnd)}\"/>");
            sb.AppendLine($"{indent}  </linearGradient>");
            fillPaint = $"url(#{fillId})";
        }

        if (theme.UseBorderGradients)
        {
            string strokeId = prefix + "-stroke-gradient";
            sb.AppendLine($"{indent}  <linearGradient id=\"{strokeId}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"0%\">");

            if (theme.BorderGradientStops is { Count: > 1 })
            {
                int stopCount = theme.BorderGradientStops.Count;
                for (int i = 0; i < stopCount; i++)
                {
                    double offset = stopCount == 1 ? 100 : i * 100.0 / (stopCount - 1);
                    sb.AppendLine($"{indent}    <stop offset=\"{offset:F0}%\" stop-color=\"{Escape(theme.BorderGradientStops[i])}\"/>");
                }
            }
            else
            {
                string strokeStart = ColorUtils.Lighten(strokeColor, Math.Max(theme.GradientStrength * 0.28, 0.03));
                string strokeEnd = ColorUtils.Blend(strokeColor, theme.AccentColor, Math.Max(theme.GradientStrength * 0.24, 0.05));
                sb.AppendLine($"{indent}    <stop offset=\"0%\" stop-color=\"{Escape(strokeStart)}\"/>");
                sb.AppendLine($"{indent}    <stop offset=\"100%\" stop-color=\"{Escape(strokeEnd)}\"/>");
            }

            sb.AppendLine($"{indent}  </linearGradient>");
            strokePaint = $"url(#{strokeId})";
        }

        sb.AppendLine($"{indent}</defs>");
    }

    internal static void AppendShadowFilterDefs(
        StringBuilder sb,
        string indent,
        string prefix,
        Theme theme,
        out string? shadowFilterId,
        bool enabled = true)
    {
        shadowFilterId = null;

        if (!enabled || !string.Equals(theme.ShadowStyle, "soft", StringComparison.OrdinalIgnoreCase))
            return;

        shadowFilterId = prefix + "-soft-shadow";
        sb.AppendLine($"{indent}<defs>");
        sb.AppendLine($"{indent}  <filter id=\"{shadowFilterId}\" x=\"-8%\" y=\"-8%\" width=\"124%\" height=\"136%\" color-interpolation-filters=\"sRGB\">");
        sb.AppendLine($"{indent}    <feGaussianBlur in=\"SourceAlpha\" stdDeviation=\"{F(theme.ShadowBlur)}\" result=\"shadow-blur\"/>");
        sb.AppendLine($"{indent}    <feOffset in=\"shadow-blur\" dx=\"{F(theme.ShadowOffsetX)}\" dy=\"{F(theme.ShadowOffsetY)}\" result=\"shadow-offset\"/>");
        sb.AppendLine($"{indent}    <feFlood flood-color=\"{Escape(theme.ShadowColor)}\" flood-opacity=\"{F(theme.ShadowOpacity)}\" result=\"shadow-color\"/>");
        sb.AppendLine($"{indent}    <feComposite in=\"shadow-color\" in2=\"shadow-offset\" operator=\"in\" result=\"shadow\"/>");
        sb.AppendLine($"{indent}    <feMerge>");
        sb.AppendLine($"{indent}      <feMergeNode in=\"shadow\"/>");
        sb.AppendLine($"{indent}      <feMergeNode in=\"SourceGraphic\"/>");
        sb.AppendLine($"{indent}    </feMerge>");
        sb.AppendLine($"{indent}  </filter>");
        sb.AppendLine($"{indent}</defs>");
    }

    internal static double? GetMetadataDouble(Node node, string key)
    {
        if (!node.Metadata.TryGetValue(key, out var value) || value is null)
            return null;

        if (value is string s)
            return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

        if (value is IConvertible convertible)
        {
            try { return convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture); }
            catch { return null; }
        }

        return null;
    }

    internal static string Escape(string? text) =>
        text is null ? string.Empty
            : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}