using System.Text;
using DiagramForge.Models;

namespace DiagramForge.Rendering;

internal static partial class SvgNodeWriter
{
    // ── Conceptual shape rendering ────────────────────────────────────────────

    private static void AppendPyramidSegmentNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string fillOpacityAttribute, string shadowAttribute)
    {
        double topWidth = SvgRenderSupport.GetMetadataDouble(node, "conceptual:pyramidTopWidth") ?? 0;
        double bottomWidth = SvgRenderSupport.GetMetadataDouble(node, "conceptual:pyramidBottomWidth") ?? node.Width;
        double topInset = (node.Width - topWidth) / 2;
        double bottomInset = (node.Width - bottomWidth) / 2;

        string points = topWidth <= 0.01
            ? $"{SvgRenderSupport.F(node.Width / 2)},0 {SvgRenderSupport.F(bottomInset + bottomWidth)},{SvgRenderSupport.F(node.Height)} {SvgRenderSupport.F(bottomInset)},{SvgRenderSupport.F(node.Height)}"
            : $"{SvgRenderSupport.F(topInset)},0 {SvgRenderSupport.F(topInset + topWidth)},0 {SvgRenderSupport.F(bottomInset + bottomWidth)},{SvgRenderSupport.F(node.Height)} {SvgRenderSupport.F(bottomInset)},{SvgRenderSupport.F(node.Height)}";

        sb.AppendLine($"""    <polygon points="{points}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"{fillOpacityAttribute}{shadowAttribute}/>""");
    }

    private static void AppendChevronNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string fillOpacityAttribute, string shadowAttribute)
    {
        int index = node.Metadata.TryGetValue("conceptual:chevronIndex", out var idxObj) && idxObj is int idx ? idx : 0;
        double tipDepth = SvgRenderSupport.GetMetadataDouble(node, "conceptual:chevronTipDepth") ?? (node.Height * 0.35);
        double w = node.Width;
        double h = node.Height;
        double midY = h / 2;

        // First chevron: flat left edge, pointed right – 5 points (pentagon).
        // Subsequent chevrons: inward V-notch on left matching previous arrow tip, pointed right – 6 points (hexagon).
        // The notch vertex at (tipDepth, midY) aligns with the tip of the preceding stage because
        // the layout overlaps bounding boxes by tipDepth.
        string points = index == 0
            ? $"0,0 {SvgRenderSupport.F(w - tipDepth)},0 {SvgRenderSupport.F(w)},{SvgRenderSupport.F(midY)} {SvgRenderSupport.F(w - tipDepth)},{SvgRenderSupport.F(h)} 0,{SvgRenderSupport.F(h)}"
            : $"0,0 {SvgRenderSupport.F(w - tipDepth)},0 {SvgRenderSupport.F(w)},{SvgRenderSupport.F(midY)} {SvgRenderSupport.F(w - tipDepth)},{SvgRenderSupport.F(h)} 0,{SvgRenderSupport.F(h)} {SvgRenderSupport.F(tipDepth)},{SvgRenderSupport.F(midY)}";

        sb.AppendLine($"""    <polygon points="{points}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"{fillOpacityAttribute}{shadowAttribute}/>""");
    }

    private static void AppendTargetRingNode(StringBuilder sb, Node node, string stroke, Theme theme, string shadowAttribute)
    {
        double strokeWidth = SvgRenderSupport.GetMetadataDouble(node, "target:ringStrokeWidth") ?? Math.Max(theme.StrokeWidth * 4, 24);
        double radius = Math.Min(node.Width, node.Height) / 2 - strokeWidth / 2;
        double centerX = node.Width / 2;
        double centerY = node.Height / 2;
        string outlineColor = "#888888";
        double outlineStrokeWidth = 0.8;
        double outerRadius = radius + (strokeWidth / 2);
        double innerRadius = radius - (strokeWidth / 2);

        sb.AppendLine($"""    <circle cx="{SvgRenderSupport.F(centerX)}" cy="{SvgRenderSupport.F(centerY)}" r="{SvgRenderSupport.F(radius)}" fill="none" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(strokeWidth)}"{shadowAttribute}/>""");

        if (innerRadius > outlineStrokeWidth / 2)
        {
            sb.AppendLine($"""    <circle cx="{SvgRenderSupport.F(centerX)}" cy="{SvgRenderSupport.F(centerY)}" r="{SvgRenderSupport.F(innerRadius)}" fill="none" stroke="{outlineColor}" stroke-width="{SvgRenderSupport.F(outlineStrokeWidth)}"/>""");
        }

        sb.AppendLine($"""    <circle cx="{SvgRenderSupport.F(centerX)}" cy="{SvgRenderSupport.F(centerY)}" r="{SvgRenderSupport.F(outerRadius)}" fill="none" stroke="{outlineColor}" stroke-width="{SvgRenderSupport.F(outlineStrokeWidth)}"/>""");
    }

    private static void AppendTargetCardNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string shadowAttribute)
    {
        double accentWidth = SvgRenderSupport.GetMetadataDouble(node, "target:accentWidth") ?? 14;
        double titleFontSize = SvgRenderSupport.GetMetadataDouble(node, "target:titleFontSize") ?? (theme.FontSize * 1.1);
        double descFontSize = SvgRenderSupport.GetMetadataDouble(node, "target:descFontSize") ?? (theme.FontSize * 0.94);
        string accentColor = node.Metadata.TryGetValue("target:accentColor", out var accentObj)
            ? accentObj as string ?? stroke
            : stroke;

        double w = node.Width;
        double h = node.Height;
        double borderRadius = theme.BorderRadius * 1.25;
        string outlineColor = "#888888";
        double outlineStrokeWidth = 0.8;
        double accentBorderStrokeWidth = Math.Max(2.4, (theme.StrokeWidth + 0.2) * 1.5);
        double accentBorderInset = (outlineStrokeWidth / 2) + (accentBorderStrokeWidth / 2);
        double accentBorderRadius = Math.Max(0, borderRadius - accentBorderInset);
        double accentInsetX = Math.Max(12, theme.NodePadding * 0.9);
        double accentInsetY = Math.Max(16, theme.NodePadding * 1.2);
        double accentHeight = Math.Max(36, h - (accentInsetY * 2));
        double accentRadius = Math.Min(accentWidth / 2, 8);

        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(w)}" height="{SvgRenderSupport.F(h)}" rx="{SvgRenderSupport.F(borderRadius)}" ry="{SvgRenderSupport.F(borderRadius)}" fill="{fill}" stroke="none"{shadowAttribute}/>""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(w)}" height="{SvgRenderSupport.F(h)}" rx="{SvgRenderSupport.F(borderRadius)}" ry="{SvgRenderSupport.F(borderRadius)}" fill="none" stroke="{outlineColor}" stroke-width="{SvgRenderSupport.F(outlineStrokeWidth)}"/>""");
        sb.AppendLine($"""    <rect x="{SvgRenderSupport.F(accentBorderInset)}" y="{SvgRenderSupport.F(accentBorderInset)}" width="{SvgRenderSupport.F(Math.Max(0, w - (accentBorderInset * 2)))}" height="{SvgRenderSupport.F(Math.Max(0, h - (accentBorderInset * 2)))}" rx="{SvgRenderSupport.F(accentBorderRadius)}" ry="{SvgRenderSupport.F(accentBorderRadius)}" fill="none" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(accentBorderStrokeWidth)}"/>""");
        sb.AppendLine($"""    <rect x="{SvgRenderSupport.F(accentInsetX)}" y="{SvgRenderSupport.F(accentInsetY)}" width="{SvgRenderSupport.F(accentWidth)}" height="{SvgRenderSupport.F(accentHeight)}" rx="{SvgRenderSupport.F(accentRadius)}" ry="{SvgRenderSupport.F(accentRadius)}" fill="{SvgRenderSupport.Escape(accentColor)}" stroke="none"/>""");

        double textX = accentInsetX + accentWidth + theme.NodePadding * 1.55;
        double titleY = theme.NodePadding * 1.15 + titleFontSize;
        string titleColor = SvgRenderSupport.Escape(node.Label.Color ?? theme.TextColor);
        string descColor = SvgRenderSupport.Escape(theme.SubtleTextColor);

        sb.AppendLine($"""    <text x="{SvgRenderSupport.F(textX)}" y="{SvgRenderSupport.F(titleY)}" text-anchor="start" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(titleFontSize)}" font-weight="bold" fill="{titleColor}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");

        if (node.Metadata.TryGetValue("target:description", out var descObj) && descObj is string description && !string.IsNullOrWhiteSpace(description))
        {
            var lines = description.Split('\n');
            double lineHeight = descFontSize * 1.45;
            double startY = titleY + descFontSize * 1.15;

            for (int i = 0; i < lines.Length; i++)
            {
                double lineY = startY + i * lineHeight;
                sb.AppendLine($"""    <text x="{SvgRenderSupport.F(textX)}" y="{SvgRenderSupport.F(lineY)}" text-anchor="start" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(descFontSize)}" fill="{descColor}">{SvgRenderSupport.Escape(lines[i])}</text>""");
            }
        }
    }

    /// <summary>
    /// Renders a tab-list card or stacked band. Dispatches to the appropriate
    /// sub-renderer based on the <c>tablist:layout</c> metadata value.
    /// </summary>
    private static void AppendTabListBandNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string shadowAttribute)
    {
        string layout = node.Metadata.TryGetValue("tablist:layout", out var lo) ? lo as string ?? "cards" : "cards";

        if (layout == "stacked")
            AppendTabListStackedBand(sb, node, fill, stroke, theme, shadowAttribute);
        else if (layout == "flat")
            AppendTabListFlatBand(sb, node, fill, stroke, theme, shadowAttribute);
        else
            AppendTabListCardBand(sb, node, fill, stroke, theme, shadowAttribute);

        // Render right-edge icon if present
        if (node.ResolvedIcon is not null)
        {
            var iconLayout = GetNodeIconLayout(node, theme);
            // Determine icon color from the content area fill for best contrast
            string iconFill = layout switch
            {
                "stacked" => node.Metadata.TryGetValue("tablist:barTextColor", out var btc) ? btc as string ?? "#FFFFFF" : "#FFFFFF",
                "flat" => node.Label.Color ?? ColorUtils.ChooseTextColor(fill),
                _ => ColorUtils.ChooseTextColor(
                    node.Metadata.TryGetValue("tablist:contentFill", out var cf) ? cf as string ?? theme.NodeFillColor : theme.NodeFillColor),
            };
            AppendNodeIcon(sb, node, SvgRenderSupport.Escape(iconFill), iconLayout);
        }
    }

    /// <summary>
    /// Cards variant: rounded content card behind, bold colored accent block on the left,
    /// bulleted description items left-aligned in the content area.
    /// </summary>
    private static void AppendTabListCardBand(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string shadowAttribute)
    {
        double accentWidth = SvgRenderSupport.GetMetadataDouble(node, "tablist:accentWidth") ?? (node.Width * 0.25);
        string contentFill = node.Metadata.TryGetValue("tablist:contentFill", out var cfObj) ? cfObj as string ?? theme.NodeFillColor : theme.NodeFillColor;
        string contentStroke = node.Metadata.TryGetValue("tablist:contentStroke", out var csObj) ? csObj as string ?? theme.NodeStrokeColor : theme.NodeStrokeColor;
        double descFontSize = SvgRenderSupport.GetMetadataDouble(node, "tablist:descFontSize") ?? theme.FontSize;
        double borderRadius = theme.BorderRadius;
        double w = node.Width;
        double h = node.Height;
        double sw = theme.StrokeWidth;

        // Content card: full-width rounded rect (drawn first, behind accent)
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(w)}" height="{SvgRenderSupport.F(h)}" rx="{SvgRenderSupport.F(borderRadius)}" ry="{SvgRenderSupport.F(borderRadius)}" fill="{SvgRenderSupport.Escape(contentFill)}" stroke="{SvgRenderSupport.Escape(contentStroke)}" stroke-width="{SvgRenderSupport.F(sw)}"{shadowAttribute}/>""");

        // Accent block: vibrant rounded rect on the left
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(accentWidth)}" height="{SvgRenderSupport.F(h)}" rx="{SvgRenderSupport.F(borderRadius)}" ry="{SvgRenderSupport.F(borderRadius)}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(sw)}"/>""");

        // Bulleted description text – left-aligned in the content area
        if (node.Metadata.TryGetValue("tablist:description", out var descObj) && descObj is string description && !string.IsNullOrWhiteSpace(description))
        {
            var descLines = description.Split('\n');
            double lineHeight = descFontSize * 1.5;
            double textBlockHeight = descLines.Length * lineHeight;
            double textX = accentWidth + theme.NodePadding * 2;
            double startY = (h - textBlockHeight) / 2 + descFontSize * 0.9;

            string descTextColor = SvgRenderSupport.Escape(
                ColorUtils.ChooseTextColor(contentFill));

            for (int i = 0; i < descLines.Length; i++)
            {
                double lineY = startY + i * lineHeight;
                sb.AppendLine($"""    <text x="{SvgRenderSupport.F(textX)}" y="{SvgRenderSupport.F(lineY)}" text-anchor="start" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(descFontSize)}" fill="{descTextColor}">{SvgRenderSupport.Escape("\u2022  " + descLines[i])}</text>""");
            }
        }
    }

    /// <summary>
    /// Stacked variant: compact colored content bar with a bold number tab on the left.
    /// Title text is rendered bold on the first line within the content bar, followed
    /// by description text on subsequent lines.
    /// </summary>
    private static void AppendTabListStackedBand(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string shadowAttribute)
    {
        double tabWidth = SvgRenderSupport.GetMetadataDouble(node, "tablist:accentWidth") ?? (node.Width * 0.15);
        string barFill = node.Metadata.TryGetValue("tablist:contentFill", out var cfObj) ? cfObj as string ?? theme.NodeFillColor : theme.NodeFillColor;
        string barStroke = node.Metadata.TryGetValue("tablist:contentStroke", out var csObj) ? csObj as string ?? "none" : "none";
        string tabStroke = node.Metadata.TryGetValue("tablist:tabStroke", out var tsObj) ? tsObj as string ?? "none" : "none";
        string barTextColor = node.Metadata.TryGetValue("tablist:barTextColor", out var btObj) ? btObj as string ?? "#FFFFFF" : "#FFFFFF";
        double descFontSize = SvgRenderSupport.GetMetadataDouble(node, "tablist:descFontSize") ?? theme.FontSize;
        double titleFontSize = SvgRenderSupport.GetMetadataDouble(node, "tablist:titleFontSize") ?? (theme.FontSize * 1.1);
        double borderRadius = theme.BorderRadius;
        double w = node.Width;
        double h = node.Height;
        double subtleStrokeWidth = 0.75;

        // Content bar: full-width colored rect with subtle border for definition
        string strokeAttr = barStroke == "none"
            ? " stroke=\"none\""
            : $" stroke=\"{SvgRenderSupport.Escape(barStroke)}\" stroke-width=\"{SvgRenderSupport.F(subtleStrokeWidth)}\"";
        sb.AppendLine($"""    <rect x="{SvgRenderSupport.F(tabWidth - borderRadius)}" y="0" width="{SvgRenderSupport.F(w - tabWidth + borderRadius)}" height="{SvgRenderSupport.F(h)}" rx="{SvgRenderSupport.F(borderRadius)}" ry="{SvgRenderSupport.F(borderRadius)}" fill="{SvgRenderSupport.Escape(barFill)}"{strokeAttr}{shadowAttribute}/>""");

        // Number tab: vivid accent block on left
        string tabStrokeAttr = tabStroke == "none"
            ? " stroke=\"none\""
            : $" stroke=\"{SvgRenderSupport.Escape(tabStroke)}\" stroke-width=\"{SvgRenderSupport.F(subtleStrokeWidth)}\"";
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(tabWidth)}" height="{SvgRenderSupport.F(h)}" rx="{SvgRenderSupport.F(borderRadius)}" ry="{SvgRenderSupport.F(borderRadius)}" fill="{fill}"{tabStrokeAttr}/>""");

        // Number text centered in the tab
        string catNumber = node.Metadata.TryGetValue("tablist:categoryNumber", out var numObj) ? numObj as string ?? "" : "";
        if (!string.IsNullOrEmpty(catNumber))
        {
            string numColor = SvgRenderSupport.Escape(node.Label.Color ?? ColorUtils.ChooseTextColor(fill));
            double numFontSize = node.Label.FontSize ?? theme.FontSize * 1.8;
            double numY = h / 2 + numFontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(tabWidth / 2)}" y="{SvgRenderSupport.F(numY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(numFontSize)}" font-weight="bold" fill="{numColor}">{SvgRenderSupport.Escape(catNumber)}</text>""");
        }

        // Title text (bold) + description lines in the content bar
        string barTextColorEscaped = SvgRenderSupport.Escape(barTextColor);
        double textX = tabWidth + theme.NodePadding * 2;
        double curY = theme.NodePadding + titleFontSize * 1.0;

        // Title line – bold, larger
        sb.AppendLine($"""    <text x="{SvgRenderSupport.F(textX)}" y="{SvgRenderSupport.F(curY)}" text-anchor="start" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(titleFontSize)}" font-weight="bold" fill="{barTextColorEscaped}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");

        // Description lines – normal weight, smaller
        if (node.Metadata.TryGetValue("tablist:description", out var descObj) && descObj is string description && !string.IsNullOrWhiteSpace(description))
        {
            var descLines = description.Split('\n');
            double lineHeight = descFontSize * 1.4;
            curY += titleFontSize * 0.6 + descFontSize * 0.8;

            for (int i = 0; i < descLines.Length; i++)
            {
                double lineY = curY + i * lineHeight;
                sb.AppendLine($"""    <text x="{SvgRenderSupport.F(textX)}" y="{SvgRenderSupport.F(lineY)}" text-anchor="start" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(descFontSize)}" fill="{barTextColorEscaped}">{SvgRenderSupport.Escape(descLines[i])}</text>""");
            }
        }
    }

    /// <summary>
    /// Flat variant: a thin vertical accent line on the left, followed by a colored title bar
    /// with bulleted description items beneath it in plain text — clean, modern whitespace-heavy style.
    /// </summary>
    private static void AppendTabListFlatBand(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string shadowAttribute)
    {
        double accentLineWidth = SvgRenderSupport.GetMetadataDouble(node, "tablist:accentLineWidth") ?? 5;
        double accentLineGap = SvgRenderSupport.GetMetadataDouble(node, "tablist:accentLineGap") ?? 12;
        string accentColor = node.Metadata.TryGetValue("tablist:accentColor", out var acObj) ? acObj as string ?? fill : fill;
        double barWidth = SvgRenderSupport.GetMetadataDouble(node, "tablist:barWidth") ?? (node.Width - accentLineWidth - accentLineGap);
        double titleBarHeight = SvgRenderSupport.GetMetadataDouble(node, "tablist:titleBarHeight") ?? (node.Height * 0.3);
        double descFontSize = SvgRenderSupport.GetMetadataDouble(node, "tablist:descFontSize") ?? theme.FontSize;
        double titleFontSize = node.Label.FontSize ?? (theme.FontSize * 1.15);
        double borderRadius = theme.BorderRadius;
        double h = node.Height;
        double barX = accentLineWidth + accentLineGap;

        // Vertical accent line (full height of this category section)
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(accentLineWidth)}" height="{SvgRenderSupport.F(h)}" rx="{SvgRenderSupport.F(accentLineWidth / 2)}" ry="{SvgRenderSupport.F(accentLineWidth / 2)}" fill="{SvgRenderSupport.Escape(accentColor)}" stroke="none"/>""");

        // Colored title bar
        sb.AppendLine($"""    <rect x="{SvgRenderSupport.F(barX)}" y="0" width="{SvgRenderSupport.F(barWidth)}" height="{SvgRenderSupport.F(titleBarHeight)}" rx="{SvgRenderSupport.F(borderRadius)}" ry="{SvgRenderSupport.F(borderRadius)}" fill="{fill}" stroke="none"{shadowAttribute}/>""");

        // Title text left-aligned inside the bar
        string titleColor = SvgRenderSupport.Escape(node.Label.Color ?? ColorUtils.ChooseTextColor(fill));
        double titleTextX = barX + theme.NodePadding * 1.5;
        double titleTextY = titleBarHeight / 2 + titleFontSize * 0.35;
        sb.AppendLine($"""    <text x="{SvgRenderSupport.F(titleTextX)}" y="{SvgRenderSupport.F(titleTextY)}" text-anchor="start" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(titleFontSize)}" font-weight="bold" fill="{titleColor}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");

        // Bulleted description items beneath the title bar
        if (node.Metadata.TryGetValue("tablist:description", out var descObj) && descObj is string description && !string.IsNullOrWhiteSpace(description))
        {
            var descLines = description.Split('\n');
            double lineHeight = descFontSize * 1.6;
            double startY = titleBarHeight + theme.NodePadding * 0.8 + descFontSize;
            string descTextColor = SvgRenderSupport.Escape(theme.TextColor);

            for (int i = 0; i < descLines.Length; i++)
            {
                double lineY = startY + i * lineHeight;
                sb.AppendLine($"""    <text x="{SvgRenderSupport.F(titleTextX)}" y="{SvgRenderSupport.F(lineY)}" text-anchor="start" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(descFontSize)}" fill="{descTextColor}">{SvgRenderSupport.Escape("\u2022  " + descLines[i])}</text>""");
            }
        }
    }

    private static void AppendFunnelSegmentNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string fillOpacityAttribute, string shadowAttribute)
    {
        double topWidth = SvgRenderSupport.GetMetadataDouble(node, "conceptual:funnelTopWidth") ?? node.Width;
        double bottomWidth = SvgRenderSupport.GetMetadataDouble(node, "conceptual:funnelBottomWidth") ?? 0;
        double topInset = (node.Width - topWidth) / 2;
        double bottomInset = (node.Width - bottomWidth) / 2;

        string points = bottomWidth <= 0.01
            ? $"{SvgRenderSupport.F(topInset)},0 {SvgRenderSupport.F(topInset + topWidth)},0 {SvgRenderSupport.F(node.Width / 2)},{SvgRenderSupport.F(node.Height)}"
            : $"{SvgRenderSupport.F(topInset)},0 {SvgRenderSupport.F(topInset + topWidth)},0 {SvgRenderSupport.F(bottomInset + bottomWidth)},{SvgRenderSupport.F(node.Height)} {SvgRenderSupport.F(bottomInset)},{SvgRenderSupport.F(node.Height)}";

        sb.AppendLine($"""    <polygon points="{points}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"{fillOpacityAttribute}{shadowAttribute}/>""");
    }
}
