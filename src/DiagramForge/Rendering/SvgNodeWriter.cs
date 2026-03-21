using System.Text;
using DiagramForge.Models;

namespace DiagramForge.Rendering;

internal static class SvgNodeWriter
{
    private const double DefaultLabelLineHeight = 1.15;
    private const double AnnotationFontSizeRatio = 0.85;
    internal const double DefaultIconSize = 48;
    internal const double IconLabelGap = 6;

    internal static void AppendNode(StringBuilder sb, Node node, Theme theme, int nodeIndex = 0)
    {
        // Hidden nodes (e.g. tablist items merged into parent band) produce no SVG output.
        if (node.Metadata.TryGetValue("render:hidden", out var hiddenVal) && hiddenVal is true)
            return;

        string? xyChartKind = node.Metadata.TryGetValue("xychart:kind", out var xyKindObj) ? xyKindObj as string : null;
        string baseFill;
        string baseStroke;

        if (SvgRenderSupport.TryResolveXyChartColors(node, theme, out string chartFill, out string chartStroke))
        {
            baseFill = chartFill;
            baseStroke = chartStroke;
        }
        else if (node.FillColor is not null)
        {
            baseFill = node.FillColor;
            baseStroke = node.StrokeColor ?? ColorUtils.Darken(node.FillColor, 0.20);
        }
        else if (theme.NodePalette is { Count: > 0 })
        {
            string paletteFill = theme.NodePalette[nodeIndex % theme.NodePalette.Count];
            baseFill = paletteFill;
            baseStroke =
                node.StrokeColor
                ?? (theme.NodeStrokePalette is { Count: > 0 }
                    ? theme.NodeStrokePalette[nodeIndex % theme.NodeStrokePalette.Count]
                    : ColorUtils.Darken(paletteFill, 0.20));
        }
        else
        {
            baseFill = theme.NodeFillColor;
            baseStroke = node.StrokeColor ?? theme.NodeStrokeColor;
        }

        double? fillOpacity = SvgRenderSupport.GetMetadataDouble(node, "render:fillOpacity");
        if (!fillOpacity.HasValue && string.Equals(xyChartKind, "bar", StringComparison.OrdinalIgnoreCase))
            fillOpacity = ColorUtils.IsLight(theme.BackgroundColor) ? 0.88 : 0.80;

        string fillOpacityAttribute = fillOpacity.HasValue
            ? $" fill-opacity=\"{SvgRenderSupport.F(fillOpacity.Value)}\""
            : string.Empty;

        bool textOnly = node.Metadata.TryGetValue("render:textOnly", out var textOnlyObj)
            && textOnlyObj is bool isTextOnly
            && isTextOnly;
        bool applyNodeShadow = theme.UseNodeShadows
            && string.Equals(theme.ShadowStyle, "soft", StringComparison.OrdinalIgnoreCase)
            && !textOnly;

        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");

        // Per-node gradient opt-out (e.g. pillars title nodes use a solid saturated fill).
        string fill, stroke;
        if (node.Metadata.TryGetValue("render:noGradient", out var ngVal) && ngVal is true)
        {
            fill = SvgRenderSupport.Escape(baseFill);
            stroke = SvgRenderSupport.Escape(baseStroke);
        }
        else
        {
            SvgRenderSupport.AppendGradientDefs(sb, "    ", $"node-{nodeIndex}", baseFill, baseStroke, theme, out fill, out stroke);
        }
        SvgRenderSupport.AppendShadowFilterDefs(sb, "    ", $"node-{nodeIndex}", theme, out string? nodeShadowFilterId, applyNodeShadow);
        string nodeShadowAttribute = nodeShadowFilterId is null ? string.Empty : $" filter=\"url(#{nodeShadowFilterId})\"";

        if (!textOnly)
        {
            if (node.Metadata.TryGetValue("conceptual:pyramidSegment", out var pyramidSegmentObj)
                && pyramidSegmentObj is bool isPyramidSegment
                && isPyramidSegment)
            {
                AppendPyramidSegmentNode(sb, node, fill, stroke, theme, fillOpacityAttribute, nodeShadowAttribute);
            }
            else if (node.Metadata.TryGetValue("conceptual:funnelSegment", out var funnelSegmentObj)
                && funnelSegmentObj is bool isFunnelSegment
                && isFunnelSegment)
            {
                AppendFunnelSegmentNode(sb, node, fill, stroke, theme, fillOpacityAttribute, nodeShadowAttribute);
            }
            else if (node.Metadata.TryGetValue("conceptual:chevronSegment", out var chevronSegObj)
                && chevronSegObj is bool isChevronSegment
                && isChevronSegment)
            {
                AppendChevronNode(sb, node, fill, stroke, theme, fillOpacityAttribute, nodeShadowAttribute);
            }
            else if (node.Metadata.TryGetValue("tablist:band", out var tablistBandObj)
                && tablistBandObj is true)
            {
                AppendTabListBandNode(sb, node, fill, stroke, theme, nodeShadowAttribute);
            }
            else if (node.Compartments.Count > 0 || node.Annotations.Count > 0)
            {
                AppendClassNode(sb, node, fill, stroke, baseFill, theme, nodeShadowAttribute);
            }
            else
            {
                switch (node.Shape)
                {
                    case Shape.Circle:
                    case Shape.Ellipse:
                        AppendEllipseNode(sb, node, fill, stroke, theme, fillOpacityAttribute, nodeShadowAttribute);
                        break;
                    case Shape.Diamond:
                        AppendDiamondNode(sb, node, fill, stroke, theme, fillOpacityAttribute, nodeShadowAttribute);
                        break;
                    case Shape.Pill:
                    case Shape.Stadium:
                        AppendRoundedRectNode(sb, node, fill, stroke, theme, fillOpacityAttribute, node.Height / 2, nodeShadowAttribute);
                        break;
                    case Shape.ArrowRight:
                        AppendArrowPolygon(sb, node.Width, node.Height, fill, stroke, theme, "right", nodeShadowAttribute);
                        break;
                    case Shape.ArrowLeft:
                        AppendArrowPolygon(sb, node.Width, node.Height, fill, stroke, theme, "left", nodeShadowAttribute);
                        break;
                    case Shape.ArrowUp:
                        AppendArrowPolygon(sb, node.Width, node.Height, fill, stroke, theme, "up", nodeShadowAttribute);
                        break;
                    case Shape.ArrowDown:
                        AppendArrowPolygon(sb, node.Width, node.Height, fill, stroke, theme, "down", nodeShadowAttribute);
                        break;
                    case Shape.Rectangle:
                        if (string.Equals(xyChartKind, "bar", StringComparison.OrdinalIgnoreCase))
                            AppendRoundedRectNode(sb, node, fill, stroke, theme, fillOpacityAttribute, SvgRenderSupport.GetXyChartBarRadius(node, theme), nodeShadowAttribute);
                        else
                            AppendRoundedRectNode(sb, node, fill, stroke, theme, fillOpacityAttribute, 0, nodeShadowAttribute);
                        break;
                    case Shape.Cloud:
                        AppendCloudPath(sb, node.Width, node.Height, fill, stroke, theme, nodeShadowAttribute);
                        break;
                    default:
                        AppendRoundedRectNode(sb, node, fill, stroke, theme, fillOpacityAttribute, theme.BorderRadius, nodeShadowAttribute);
                        break;
                }
            }
        }
        else if (SvgRenderSupport.HasTextOnlyBackdrop(node, fillOpacity))
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            var lines = GetRenderedLabelLines(node.Label);
            double textWidth = lines.Length == 0 ? 0 : lines.Max(line => SvgRenderSupport.EstimateTextWidth(line, fontSize));
            double horizontalPadding = theme.NodePadding * 0.4;
            double lineHeight = fontSize * DefaultLabelLineHeight;
            double textBlockHeight = lines.Length == 0 ? fontSize : fontSize + (lines.Length - 1) * lineHeight;
            double top = -textBlockHeight * 0.65;
            double height = textBlockHeight + fontSize * 0.25;
            double width = textWidth + horizontalPadding * 2;
            sb.AppendLine($"""    <rect x="{SvgRenderSupport.F(-width / 2)}" y="{SvgRenderSupport.F(top)}" width="{SvgRenderSupport.F(width)}" height="{SvgRenderSupport.F(height)}" rx="{SvgRenderSupport.F(fontSize * 0.35)}" ry="{SvgRenderSupport.F(fontSize * 0.35)}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"{fillOpacityAttribute}/>""");
        }

        string resolvedTextColor = SvgRenderSupport.Escape(
            node.Label.Color ?? SvgRenderSupport.ResolveNodeTextColor(baseFill, theme));

        // ── Icon rendering ────────────────────────────────────────────────────
        bool isTabListBand = node.Metadata.TryGetValue("tablist:band", out var tlbObj) && tlbObj is true;
        bool hasIcon = node.ResolvedIcon is not null;
        double iconAreaHeight = 0;
        if (hasIcon && !textOnly && !isTabListBand)
        {
            var iconLayout = GetNodeIconLayout(node, theme);
            AppendNodeIcon(sb, node, resolvedTextColor, iconLayout);
            iconAreaHeight = iconLayout.Size + IconLabelGap;
        }

        if (!string.IsNullOrWhiteSpace(node.Label.Text)
            && node.Compartments.Count == 0 && node.Annotations.Count == 0
            && !(node.Metadata.TryGetValue("render:suppressLabel", out var suppressObj) && suppressObj is true))
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            double textX = SvgRenderSupport.GetMetadataDouble(node, "label:centerX") ?? (textOnly ? 0 : (node.Width / 2));
            double textBaselineY = SvgRenderSupport.GetMetadataDouble(node, "label:centerY") ?? (textOnly ? 0 : (node.Height / 2));

            // Shift label down when icon is present (icon sits in the upper area).
            if (hasIcon && !textOnly)
                textBaselineY += iconAreaHeight / 2;

            AppendNodeLabel(sb, node.Label, theme, textX, textBaselineY, fontSize, resolvedTextColor);
        }

        sb.AppendLine("  </g>");
    }

    private static void AppendNodeLabel(
        StringBuilder sb,
        Label label,
        Theme theme,
        double centerX,
        double centerY,
        double fontSize,
        string textColor)
    {
        var lines = GetRenderedLabelLines(label);
        double lineHeight = fontSize * DefaultLabelLineHeight;
        double firstBaselineY = centerY + fontSize * 0.35 - ((lines.Length - 1) * lineHeight / 2);

        string fontWeightAttr = !string.IsNullOrEmpty(label.FontWeight) ? $" font-weight=\"{SvgRenderSupport.Escape(label.FontWeight)}\"" : "";

        if (lines.Length == 1)
        {
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(centerX)}" y="{SvgRenderSupport.F(firstBaselineY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}"{fontWeightAttr} fill="{textColor}">{SvgRenderSupport.Escape(lines[0])}</text>""");
            return;
        }

        sb.AppendLine($"""    <text x="{SvgRenderSupport.F(centerX)}" y="{SvgRenderSupport.F(firstBaselineY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}"{fontWeightAttr} fill="{textColor}">""");
        sb.AppendLine($"""      <tspan x="{SvgRenderSupport.F(centerX)}" y="{SvgRenderSupport.F(firstBaselineY)}">{SvgRenderSupport.Escape(lines[0])}</tspan>""");

        for (int i = 1; i < lines.Length; i++)
            sb.AppendLine($"""      <tspan x="{SvgRenderSupport.F(centerX)}" dy="{SvgRenderSupport.F(lineHeight)}">{SvgRenderSupport.Escape(lines[i])}</tspan>""");

        sb.AppendLine("    </text>");
    }

    /// <summary>
    /// Renders a resolved icon inside a node, centered horizontally and positioned
    /// in the upper portion to leave room for the label below.
    /// </summary>
    /// <param name="resolvedTextColor">
    /// The already-resolved text color for the node (same value used for the label),
    /// ensuring icon and label use a consistent, contrast-aware color.
    /// </param>
    private static void AppendNodeIcon(StringBuilder sb, Node node, string resolvedTextColor, NodeIconLayout iconLayout)
    {
        var icon = node.ResolvedIcon;
        if (icon is null)
            return;

        // Parse viewBox to get the source coordinate system.
        string[] viewBoxParts = icon.ViewBox.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string viewBox = viewBoxParts.Length == 4 ? icon.ViewBox : "0 0 24 24";

        // Use the same resolved text color as the label so icon and label are always consistent,
        // including when the node fill is sourced from a theme palette.
        sb.AppendLine($"""    <g transform="translate({SvgRenderSupport.F(iconLayout.X)},{SvgRenderSupport.F(iconLayout.Y)})">""");
        sb.AppendLine($"""      <svg width="{SvgRenderSupport.F(iconLayout.Size)}" height="{SvgRenderSupport.F(iconLayout.Size)}" viewBox="{SvgRenderSupport.Escape(viewBox)}" overflow="visible" color="{resolvedTextColor}">""");
        sb.AppendLine($"        {icon.SvgContent}");
        sb.AppendLine("      </svg>");
        sb.AppendLine("    </g>");
    }

    private static NodeIconLayout GetNodeIconLayout(Node node, Theme theme)
    {
        double size = SvgRenderSupport.GetMetadataDouble(node, "icon:size") ?? DefaultIconSize;
        double defaultCenterX = SvgRenderSupport.GetMetadataDouble(node, "label:centerX") ?? (node.Width / 2);
        double x = SvgRenderSupport.GetMetadataDouble(node, "icon:x") ?? (defaultCenterX - size / 2);
        double y = SvgRenderSupport.GetMetadataDouble(node, "icon:y") ?? theme.NodePadding;
        return new NodeIconLayout(x, y, size);
    }

    private readonly record struct NodeIconLayout(double X, double Y, double Size);

    private static string[] GetRenderedLabelLines(Label label) =>
        label.Lines is { Length: > 0 }
            ? label.Lines
            : label.Text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');

    private static void AppendArrowPolygon(StringBuilder sb, double width, double height, string fill, string stroke, Theme theme, string direction, string shadowAttribute)
    {
        double head = Math.Min(width, height) * 0.4;
        string points = direction switch
        {
            "left" => $"{SvgRenderSupport.F(width)},{SvgRenderSupport.F(height * 0.2)} {SvgRenderSupport.F(head)},{SvgRenderSupport.F(height * 0.2)} {SvgRenderSupport.F(head)},{SvgRenderSupport.F(0)} 0,{SvgRenderSupport.F(height / 2)} {SvgRenderSupport.F(head)},{SvgRenderSupport.F(height)} {SvgRenderSupport.F(head)},{SvgRenderSupport.F(height * 0.8)} {SvgRenderSupport.F(width)},{SvgRenderSupport.F(height * 0.8)}",
            "up" => $"{SvgRenderSupport.F(width * 0.2)},{SvgRenderSupport.F(height)} {SvgRenderSupport.F(width * 0.2)},{SvgRenderSupport.F(head)} 0,{SvgRenderSupport.F(head)} {SvgRenderSupport.F(width / 2)},0 {SvgRenderSupport.F(width)},{SvgRenderSupport.F(head)} {SvgRenderSupport.F(width * 0.8)},{SvgRenderSupport.F(head)} {SvgRenderSupport.F(width * 0.8)},{SvgRenderSupport.F(height)}",
            "down" => $"{SvgRenderSupport.F(width * 0.2)},0 {SvgRenderSupport.F(width * 0.2)},{SvgRenderSupport.F(height - head)} 0,{SvgRenderSupport.F(height - head)} {SvgRenderSupport.F(width / 2)},{SvgRenderSupport.F(height)} {SvgRenderSupport.F(width)},{SvgRenderSupport.F(height - head)} {SvgRenderSupport.F(width * 0.8)},{SvgRenderSupport.F(height - head)} {SvgRenderSupport.F(width * 0.8)},0",
            _ => $"0,{SvgRenderSupport.F(height * 0.2)} {SvgRenderSupport.F(width - head)},{SvgRenderSupport.F(height * 0.2)} {SvgRenderSupport.F(width - head)},0 {SvgRenderSupport.F(width)},{SvgRenderSupport.F(height / 2)} {SvgRenderSupport.F(width - head)},{SvgRenderSupport.F(height)} {SvgRenderSupport.F(width - head)},{SvgRenderSupport.F(height * 0.8)} 0,{SvgRenderSupport.F(height * 0.8)}",
        };

        sb.AppendLine($"""    <polygon points="{points}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"{shadowAttribute}/>""");
    }

    private static void AppendEllipseNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string fillOpacityAttribute, string shadowAttribute)
    {
        double cx = node.Width / 2;
        double cy = node.Height / 2;
        sb.AppendLine($"""    <ellipse cx="{SvgRenderSupport.F(cx)}" cy="{SvgRenderSupport.F(cy)}" rx="{SvgRenderSupport.F(cx)}" ry="{SvgRenderSupport.F(cy)}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"{fillOpacityAttribute}{shadowAttribute}/>""");
    }

    private static void AppendDiamondNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string fillOpacityAttribute, string shadowAttribute)
    {
        double mx = node.Width / 2;
        double my = node.Height / 2;
        sb.AppendLine($"""    <polygon points="{SvgRenderSupport.F(mx)},0 {SvgRenderSupport.F(node.Width)},{SvgRenderSupport.F(my)} {SvgRenderSupport.F(mx)},{SvgRenderSupport.F(node.Height)} 0,{SvgRenderSupport.F(my)}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"{fillOpacityAttribute}{shadowAttribute}/>""");
    }

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
        double w = node.Width;
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

    private static void AppendRoundedRectNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string fillOpacityAttribute, double radius, string shadowAttribute)
    {
        double strokeWidth = SvgRenderSupport.GetMetadataDouble(node, "render:strokeWidth") ?? theme.StrokeWidth;
        string formattedRadius = radius == 0 ? "0" : SvgRenderSupport.F(radius);
        sb.AppendLine($"""    <rect width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{formattedRadius}" ry="{formattedRadius}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(strokeWidth)}"{fillOpacityAttribute}{shadowAttribute}/>""");
    }

    private static void AppendCloudPath(StringBuilder sb, double width, double height, string fill, string stroke, Theme theme, string shadowAttribute)
    {
        double w = width;
        double h = height;
        double r1 = h * 0.28;
        double r2 = h * 0.22;
        double r3 = h * 0.30;
        double r4 = h * 0.24;
        double r5 = h * 0.20;
        double flatBottomY = h * 0.72;

        string d =
            $"M {SvgRenderSupport.F(w * 0.10)},{SvgRenderSupport.F(flatBottomY)} " +
            $"A {SvgRenderSupport.F(r1)},{SvgRenderSupport.F(r1)} 0 0,1 {SvgRenderSupport.F(w * 0.20)},{SvgRenderSupport.F(flatBottomY - r1 * 1.1)} " +
            $"A {SvgRenderSupport.F(r2)},{SvgRenderSupport.F(r2)} 0 0,1 {SvgRenderSupport.F(w * 0.37)},{SvgRenderSupport.F(flatBottomY - r2 * 1.5)} " +
            $"A {SvgRenderSupport.F(r3)},{SvgRenderSupport.F(r3)} 0 0,1 {SvgRenderSupport.F(w * 0.60)},{SvgRenderSupport.F(flatBottomY - r3 * 1.0)} " +
            $"A {SvgRenderSupport.F(r4)},{SvgRenderSupport.F(r4)} 0 0,1 {SvgRenderSupport.F(w * 0.80)},{SvgRenderSupport.F(flatBottomY - r4 * 0.9)} " +
            $"A {SvgRenderSupport.F(r5)},{SvgRenderSupport.F(r5)} 0 0,1 {SvgRenderSupport.F(w * 0.92)},{SvgRenderSupport.F(flatBottomY)} " +
            "Z";
        sb.AppendLine($"""    <path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"{shadowAttribute}/>""");
    }

    /// <summary>
    /// Renders a UML-style class box with optional stereotype annotations, a centered class name,
    /// horizontal divider lines, and left-aligned compartment content.
    /// </summary>
    private static void AppendClassNode(
        StringBuilder sb,
        Node node,
        string fill,
        string stroke,
        string baseFill,
        Theme theme,
        string shadowAttribute)
    {
        double fontSize = node.Label.FontSize ?? theme.FontSize;
        double defaultAnnotationFontSize = fontSize * AnnotationFontSizeRatio;
        double defaultLineHeight = fontSize * DefaultLabelLineHeight;
        double pad = theme.NodePadding;
        double compPad = pad / 2;
        double strokeWidth = theme.StrokeWidth;
        double centerX = node.Width / 2;
        double currentY = pad;
        string textColor = SvgRenderSupport.Escape(
            node.Label.Color ?? SvgRenderSupport.ResolveNodeTextColor(baseFill, theme));

        sb.AppendLine($"""    <rect width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="0" ry="0" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(strokeWidth)}"{shadowAttribute}/>""");

        foreach (var annotation in node.Annotations)
        {
            double annotationFontSize = annotation.FontSize ?? defaultAnnotationFontSize;
            string annotationColor = SvgRenderSupport.Escape(annotation.Color ?? textColor);

            foreach (var annotationLine in GetRenderedLabelLines(annotation))
            {
                double baseline = currentY + annotationFontSize * 0.75;
                string stereotypeText = $"\u00AB{SvgRenderSupport.Escape(annotationLine)}\u00BB";
                sb.AppendLine($"""    <text x="{SvgRenderSupport.F(centerX)}" y="{SvgRenderSupport.F(baseline)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(annotationFontSize)}" font-style="italic" fill="{annotationColor}">{stereotypeText}</text>""");
                currentY += annotationFontSize * DefaultLabelLineHeight;
            }
        }

        if (node.Annotations.Count > 0)
            currentY += compPad;

        var classNameLines = GetRenderedLabelLines(node.Label);
        double classNameBaseline = currentY + fontSize * 0.75;
        if (classNameLines.Length == 1)
        {
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(centerX)}" y="{SvgRenderSupport.F(classNameBaseline)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" font-weight="bold" fill="{textColor}">{SvgRenderSupport.Escape(classNameLines[0])}</text>""");
        }
        else
        {
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(centerX)}" y="{SvgRenderSupport.F(classNameBaseline)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" font-weight="bold" fill="{textColor}">""");
            sb.AppendLine($"""      <tspan x="{SvgRenderSupport.F(centerX)}" y="{SvgRenderSupport.F(classNameBaseline)}">{SvgRenderSupport.Escape(classNameLines[0])}</tspan>""");
            for (int i = 1; i < classNameLines.Length; i++)
                sb.AppendLine($"""      <tspan x="{SvgRenderSupport.F(centerX)}" dy="{SvgRenderSupport.F(fontSize * DefaultLabelLineHeight)}">{SvgRenderSupport.Escape(classNameLines[i])}</tspan>""");
            sb.AppendLine("    </text>");
        }

        double headerHeight = SvgRenderSupport.GetMetadataDouble(node, "class:headerHeight")
            ?? (currentY + GetRenderedLabelLines(node.Label).Length * fontSize * DefaultLabelLineHeight + pad);
        currentY = headerHeight;

        foreach (var compartment in node.Compartments)
        {
            sb.AppendLine($"""    <line x1="0" y1="{SvgRenderSupport.F(currentY)}" x2="{SvgRenderSupport.F(node.Width)}" y2="{SvgRenderSupport.F(currentY)}" stroke="{SvgRenderSupport.Escape(stroke)}" stroke-width="{SvgRenderSupport.F(strokeWidth)}"/>""");
            currentY += strokeWidth;
            currentY += compPad;

            if (compartment.Lines.Count == 0)
            {
                currentY += defaultLineHeight;
            }
            else
            {
                foreach (var line in compartment.Lines)
                {
                    double lineFontSize = line.FontSize ?? fontSize;
                    string lineColor = SvgRenderSupport.Escape(line.Color ?? textColor);
                    double lineBaseline = currentY + lineFontSize * 0.75;
                    var renderedLines = GetRenderedLabelLines(line);
                    foreach (var renderedLine in renderedLines)
                    {
                        sb.AppendLine($"""    <text x="{SvgRenderSupport.F(pad)}" y="{SvgRenderSupport.F(lineBaseline)}" text-anchor="start" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(lineFontSize)}" fill="{lineColor}">{SvgRenderSupport.Escape(renderedLine)}</text>""");
                        lineBaseline += lineFontSize * DefaultLabelLineHeight;
                    }

                    currentY += renderedLines.Length * lineFontSize * DefaultLabelLineHeight;
                }
            }

            currentY += compPad;
        }
    }
}