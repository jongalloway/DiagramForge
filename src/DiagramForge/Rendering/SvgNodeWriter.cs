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

        // Wireframe nodes use a fully custom rendering path.
        if (node.Metadata.TryGetValue("wireframe:kind", out var wfKindObj) && wfKindObj is string wfKind)
        {
            AppendWireframeNode(sb, node, wfKind, theme);
            return;
        }

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
            && (string.Equals(theme.ShadowStyle, "soft", StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme.ShadowStyle, "glow", StringComparison.OrdinalIgnoreCase))
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
            else if (node.Metadata.TryGetValue("target:kind", out var targetKindObj)
                && targetKindObj is string targetKind
                && string.Equals(targetKind, "ring", StringComparison.Ordinal))
            {
                AppendTargetRingNode(sb, node, stroke, theme, nodeShadowAttribute);
            }
            else if (node.Metadata.TryGetValue("target:kind", out targetKindObj)
                && targetKindObj is string cardKind
                && string.Equals(cardKind, "card", StringComparison.Ordinal))
            {
                AppendTargetCardNode(sb, node, fill, stroke, theme, nodeShadowAttribute);
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

    // ── Wireframe rendering ───────────────────────────────────────────────────

    private const double WfStroke = 1.2;
    private const double WfRadius = 4.0;
    private const double WfCheckmarkInsetRatio = 0.2;  // relative inset for tick path endpoints
    private const double WfDropdownChevronSize = 4.5;  // half-width of the dropdown chevron arrow

    /// <summary>
    /// Wireframe color palette derived from four semantic theme colors:
    /// <see cref="Theme.BackgroundColor"/>, <see cref="Theme.TextColor"/>,
    /// <see cref="Theme.AccentColor"/>, and <see cref="Theme.SubtleTextColor"/>.
    /// </summary>
    private readonly record struct WireframePalette
    {
        public string CardFill { get; init; }
        public string CardBorder { get; init; }
        public string HeaderFill { get; init; }
        public string ButtonFill { get; init; }
        public string ButtonText { get; init; }
        public string InputBorder { get; init; }
        public string InputBg { get; init; }
        public string InputPlaceholder { get; init; }
        public string TextColor { get; init; }
        public string SubtleText { get; init; }
        public string DividerColor { get; init; }
        public string BadgeFill { get; init; }
        public string BadgeBorder { get; init; }
        public string BadgeText { get; init; }
        public string ImageBg { get; init; }
        public string ImageBorder { get; init; }
        public string ImageX { get; init; }
        public string TabActiveFill { get; init; }
        public string TabInactiveFill { get; init; }
        public string TabBorder { get; init; }
        public string TabActiveText { get; init; }
        public string TabInactiveText { get; init; }
        public string CheckboxBorder { get; init; }
        public string CheckColor { get; init; }
        public string ToggleOnFill { get; init; }
        public string ToggleOffFill { get; init; }
        public string KnobFill { get; init; }

        public static WireframePalette FromTheme(Theme theme)
        {
            string bg = theme.BackgroundColor;
            string fg = theme.TextColor;
            string muted = theme.SubtleTextColor;
            string accent = theme.AccentColor;
            bool isLight = ColorUtils.IsLight(bg);

            // Strong foreground (buttons, check marks)
            string strong = ColorUtils.Blend(fg, bg, 0.15);
            // Inverse text (button labels, toggle knob)
            string inverse = isLight
                ? ColorUtils.Blend(bg, "#FFFFFF", 0.05)
                : ColorUtils.Blend(bg, "#000000", 0.05);

            return new WireframePalette
            {
                CardFill = ColorUtils.Blend(bg, fg, isLight ? 0.02 : 0.06),
                CardBorder = ColorUtils.Blend(muted, bg, 0.40),
                HeaderFill = ColorUtils.Blend(bg, fg, isLight ? 0.08 : 0.12),
                ButtonFill = strong,
                ButtonText = inverse,
                InputBorder = muted,
                InputBg = isLight ? ColorUtils.Blend(bg, "#FFFFFF", 0.30) : ColorUtils.Blend(bg, fg, 0.06),
                InputPlaceholder = ColorUtils.Blend(muted, bg, 0.20),
                TextColor = fg,
                SubtleText = muted,
                DividerColor = ColorUtils.Blend(muted, bg, 0.55),
                BadgeFill = ColorUtils.Blend(bg, accent, isLight ? 0.12 : 0.18),
                BadgeBorder = ColorUtils.Blend(accent, bg, 0.40),
                BadgeText = ColorUtils.Blend(accent, fg, 0.30),
                ImageBg = ColorUtils.Blend(bg, muted, 0.15),
                ImageBorder = ColorUtils.Blend(muted, bg, 0.20),
                ImageX = ColorUtils.Blend(muted, bg, 0.30),
                TabActiveFill = bg,
                TabInactiveFill = ColorUtils.Blend(bg, fg, isLight ? 0.08 : 0.12),
                TabBorder = ColorUtils.Blend(muted, bg, 0.35),
                TabActiveText = fg,
                TabInactiveText = muted,
                CheckboxBorder = ColorUtils.Blend(fg, bg, 0.50),
                CheckColor = ColorUtils.Blend(fg, bg, 0.10),
                ToggleOnFill = ColorUtils.Blend(fg, bg, 0.35),
                ToggleOffFill = ColorUtils.Blend(muted, bg, 0.40),
                KnobFill = inverse,
            };
        }
    }

    private static void AppendWireframeNode(StringBuilder sb, Node node, string kind, Theme theme)
    {
        var p = WireframePalette.FromTheme(theme);

        switch (kind)
        {
            case "column":
            case "row":
                // Layout-only containers — invisible, no SVG output.
                if (node.Metadata.ContainsKey("wireframe:isRoot"))
                    return;
                // Non-root column/row: also invisible (pure layout helpers).
                return;

            case "card":
                AppendWfCard(sb, node, theme, p.CardFill, p.CardBorder, p, isHeader: false);
                break;

            case "header":
                AppendWfCard(sb, node, theme, p.HeaderFill, p.CardBorder, p, isHeader: true);
                break;

            case "footer":
                AppendWfCard(sb, node, theme, p.HeaderFill, p.CardBorder, p, isHeader: false);
                break;

            case "button":
                AppendWfButton(sb, node, theme, p);
                break;

            case "textinput":
                AppendWfTextInput(sb, node, theme, p);
                break;

            case "checkbox":
                AppendWfCheckbox(sb, node, theme, p);
                break;

            case "radio":
                AppendWfRadio(sb, node, theme, p);
                break;

            case "toggle":
                AppendWfToggle(sb, node, theme, p);
                break;

            case "dropdown":
                AppendWfDropdown(sb, node, theme, p);
                break;

            case "tabs":
                AppendWfTabs(sb, node, theme, p);
                break;

            case "badge":
                AppendWfBadge(sb, node, theme, p);
                break;

            case "image":
                AppendWfImage(sb, node, theme, p);
                break;

            case "divider":
                AppendWfDivider(sb, node, theme, p);
                break;

            case "heading":
                AppendWfHeading(sb, node, theme, p);
                break;

            case "text":
                AppendWfText(sb, node, theme, p);
                break;
        }
    }

    // Card / Header / Footer surface

    private static void AppendWfCard(StringBuilder sb, Node node, Theme theme, string fill, string border, WireframePalette p, bool isHeader)
    {
        string rx = SvgRenderSupport.F(isHeader ? 0 : WfRadius);
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{rx}" ry="{rx}" fill="{SvgRenderSupport.Escape(fill)}" stroke="{SvgRenderSupport.Escape(border)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double labelY = fontSize + 8;
            double labelX = 12;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(labelX)}" y="{SvgRenderSupport.F(labelY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" font-weight="bold" fill="{SvgRenderSupport.Escape(p.SubtleText)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Button

    private static void AppendWfButton(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{SvgRenderSupport.F(WfRadius)}" ry="{SvgRenderSupport.F(WfRadius)}" fill="{SvgRenderSupport.Escape(p.ButtonFill)}" stroke="none"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize;
            double textX = node.Width / 2;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(textX)}" y="{SvgRenderSupport.F(textY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.ButtonText)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Text input

    private static void AppendWfTextInput(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{SvgRenderSupport.F(WfRadius)}" ry="{SvgRenderSupport.F(WfRadius)}" fill="{SvgRenderSupport.Escape(p.InputBg)}" stroke="{SvgRenderSupport.Escape(p.InputBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="8" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.InputPlaceholder)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Checkbox

    private static void AppendWfCheckbox(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        const double boxSize = 14;
        bool isChecked = node.Metadata.TryGetValue("wireframe:checked", out var cv) && cv is true;

        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");

        double topY = (node.Height - boxSize) / 2;
        sb.AppendLine($"""    <rect x="0" y="{SvgRenderSupport.F(topY)}" width="{SvgRenderSupport.F(boxSize)}" height="{SvgRenderSupport.F(boxSize)}" rx="2" ry="2" fill="{SvgRenderSupport.Escape(p.InputBg)}" stroke="{SvgRenderSupport.Escape(p.CheckboxBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        if (isChecked)
        {
            double cx = boxSize / 2;
            double cy = topY + boxSize / 2;
            // Checkmark tick: two-segment polyline (left-bottom valley → right-top peak)
            double inset = boxSize * WfCheckmarkInsetRatio;
            sb.AppendLine($"""    <polyline points="{SvgRenderSupport.F(inset)},{SvgRenderSupport.F(cy)} {SvgRenderSupport.F(cx * 0.75)},{SvgRenderSupport.F(topY + boxSize - inset)} {SvgRenderSupport.F(boxSize - inset)},{SvgRenderSupport.F(topY + inset)}" fill="none" stroke="{SvgRenderSupport.Escape(p.CheckColor)}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>""");
        }

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(boxSize + 6)}" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.TextColor)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Radio button

    private static void AppendWfRadio(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        const double circleR = 7;
        const double dotR = 3.5;
        bool isChecked = node.Metadata.TryGetValue("wireframe:checked", out var cv) && cv is true;

        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");

        double cy = node.Height / 2;
        sb.AppendLine($"""    <circle cx="{SvgRenderSupport.F(circleR)}" cy="{SvgRenderSupport.F(cy)}" r="{SvgRenderSupport.F(circleR)}" fill="{SvgRenderSupport.Escape(p.InputBg)}" stroke="{SvgRenderSupport.Escape(p.CheckboxBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        if (isChecked)
            sb.AppendLine($"""    <circle cx="{SvgRenderSupport.F(circleR)}" cy="{SvgRenderSupport.F(cy)}" r="{SvgRenderSupport.F(dotR)}" fill="{SvgRenderSupport.Escape(p.CheckColor)}"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double textY = cy + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(circleR * 2 + 6)}" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.TextColor)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Toggle

    private static void AppendWfToggle(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        bool isOn = node.Metadata.TryGetValue("wireframe:on", out var ov) && ov is true;

        const double pillW = 44;
        const double pillH = 22;
        const double knobR = 9;
        string pillFill = isOn ? p.ToggleOnFill : p.ToggleOffFill;
        double knobX = isOn ? (pillW - knobR - 3) : (knobR + 3);
        double knobY = pillH / 2;

        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        double pillTop = (node.Height - pillH) / 2;
        sb.AppendLine($"""    <rect x="0" y="{SvgRenderSupport.F(pillTop)}" width="{SvgRenderSupport.F(pillW)}" height="{SvgRenderSupport.F(pillH)}" rx="{SvgRenderSupport.F(pillH / 2)}" ry="{SvgRenderSupport.F(pillH / 2)}" fill="{SvgRenderSupport.Escape(pillFill)}" stroke="none"/>""");
        sb.AppendLine($"""    <circle cx="{SvgRenderSupport.F(knobX)}" cy="{SvgRenderSupport.F(pillTop + knobY)}" r="{SvgRenderSupport.F(knobR)}" fill="{SvgRenderSupport.Escape(p.KnobFill)}" stroke="{SvgRenderSupport.Escape(p.CardBorder)}" stroke-width="0.8"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(pillW + 8)}" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.TextColor)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Dropdown

    private static void AppendWfDropdown(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{SvgRenderSupport.F(WfRadius)}" ry="{SvgRenderSupport.F(WfRadius)}" fill="{SvgRenderSupport.Escape(p.InputBg)}" stroke="{SvgRenderSupport.Escape(p.InputBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        // Chevron icon on the right
        double chevX = node.Width - 18;
        double chevY = node.Height / 2;
        sb.AppendLine($"""    <polyline points="{SvgRenderSupport.F(chevX - WfDropdownChevronSize)},{SvgRenderSupport.F(chevY - WfDropdownChevronSize * 0.6)} {SvgRenderSupport.F(chevX)},{SvgRenderSupport.F(chevY + WfDropdownChevronSize * 0.6)} {SvgRenderSupport.F(chevX + WfDropdownChevronSize)},{SvgRenderSupport.F(chevY - WfDropdownChevronSize * 0.6)}" fill="none" stroke="{SvgRenderSupport.Escape(p.SubtleText)}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>""");

        // Vertical separator line before chevron
        sb.AppendLine($"""    <line x1="{SvgRenderSupport.F(node.Width - 28)}" y1="5" x2="{SvgRenderSupport.F(node.Width - 28)}" y2="{SvgRenderSupport.F(node.Height - 5)}" stroke="{SvgRenderSupport.Escape(p.InputBorder)}" stroke-width="0.8"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.9;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="8" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.TextColor)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Tab bar

    private static void AppendWfTabs(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        string[]? tabs = node.Metadata.TryGetValue("wireframe:tabs", out var tabsObj) ? tabsObj as string[] : null;
        int activeTab = node.Metadata.TryGetValue("wireframe:activeTab", out var atObj) && atObj is int at ? at : 0;

        if (tabs is null || tabs.Length == 0)
            return;

        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");

        double tabFontSize = theme.FontSize * 0.9;
        double tabW = node.Width / tabs.Length;

        // Bottom border line for the whole bar
        sb.AppendLine($"""    <line x1="0" y1="{SvgRenderSupport.F(node.Height)}" x2="{SvgRenderSupport.F(node.Width)}" y2="{SvgRenderSupport.F(node.Height)}" stroke="{SvgRenderSupport.Escape(p.TabBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        for (int i = 0; i < tabs.Length; i++)
        {
            bool isActive = i == activeTab;
            double tx = i * tabW;
            string tabFill = isActive ? p.TabActiveFill : p.TabInactiveFill;
            string tabText = isActive ? p.TabActiveText : p.TabInactiveText;
            string tabBotStroke = isActive ? p.TabActiveFill : p.TabBorder;

            sb.AppendLine($"""    <rect x="{SvgRenderSupport.F(tx)}" y="0" width="{SvgRenderSupport.F(tabW)}" height="{SvgRenderSupport.F(node.Height)}" fill="{SvgRenderSupport.Escape(tabFill)}" stroke="{SvgRenderSupport.Escape(p.TabBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

            // Cover the bottom border for the active tab
            if (isActive)
                sb.AppendLine($"""    <line x1="{SvgRenderSupport.F(tx + 1)}" y1="{SvgRenderSupport.F(node.Height)}" x2="{SvgRenderSupport.F(tx + tabW - 1)}" y2="{SvgRenderSupport.F(node.Height)}" stroke="{SvgRenderSupport.Escape(tabBotStroke)}" stroke-width="{SvgRenderSupport.F(WfStroke + 0.5)}"/>""");

            double textX = tx + tabW / 2;
            double textY = node.Height / 2 + tabFontSize * 0.35;
            string fontWeight = isActive ? " font-weight=\"bold\"" : string.Empty;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(textX)}" y="{SvgRenderSupport.F(textY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(tabFontSize)}"{fontWeight} fill="{SvgRenderSupport.Escape(tabText)}">{SvgRenderSupport.Escape(tabs[i])}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Badge

    private static void AppendWfBadge(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        double rx = node.Height / 2;
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{SvgRenderSupport.F(rx)}" ry="{SvgRenderSupport.F(rx)}" fill="{SvgRenderSupport.Escape(p.BadgeFill)}" stroke="{SvgRenderSupport.Escape(p.BadgeBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke * 0.8)}"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.8;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(node.Width / 2)}" y="{SvgRenderSupport.F(textY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.BadgeText)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Image placeholder

    private static void AppendWfImage(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        sb.AppendLine($"""  <g transform="translate({SvgRenderSupport.F(node.X)},{SvgRenderSupport.F(node.Y)})">""");
        sb.AppendLine($"""    <rect x="0" y="0" width="{SvgRenderSupport.F(node.Width)}" height="{SvgRenderSupport.F(node.Height)}" rx="{SvgRenderSupport.F(WfRadius)}" ry="{SvgRenderSupport.F(WfRadius)}" fill="{SvgRenderSupport.Escape(p.ImageBg)}" stroke="{SvgRenderSupport.Escape(p.ImageBorder)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");

        // Diagonal cross lines
        sb.AppendLine($"""    <line x1="0" y1="0" x2="{SvgRenderSupport.F(node.Width)}" y2="{SvgRenderSupport.F(node.Height)}" stroke="{SvgRenderSupport.Escape(p.ImageX)}" stroke-width="1"/>""");
        sb.AppendLine($"""    <line x1="{SvgRenderSupport.F(node.Width)}" y1="0" x2="0" y2="{SvgRenderSupport.F(node.Height)}" stroke="{SvgRenderSupport.Escape(p.ImageX)}" stroke-width="1"/>""");

        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            double fontSize = theme.FontSize * 0.85;
            double textY = node.Height / 2 + fontSize * 0.35;
            sb.AppendLine($"""    <text x="{SvgRenderSupport.F(node.Width / 2)}" y="{SvgRenderSupport.F(textY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{SvgRenderSupport.Escape(p.SubtleText)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
        }

        sb.AppendLine("  </g>");
    }

    // Divider

    private static void AppendWfDivider(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        double midY = node.Y + node.Height / 2;
        sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(node.X)}" y1="{SvgRenderSupport.F(midY)}" x2="{SvgRenderSupport.F(node.X + node.Width)}" y2="{SvgRenderSupport.F(midY)}" stroke="{SvgRenderSupport.Escape(p.DividerColor)}" stroke-width="{SvgRenderSupport.F(WfStroke)}"/>""");
    }

    // Heading

    private static void AppendWfHeading(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        if (string.IsNullOrWhiteSpace(node.Label.Text))
            return;

        int level = node.Metadata.TryGetValue("wireframe:headingLevel", out var lvObj) && lvObj is int lv ? lv : 1;
        double fontSize = node.Label.FontSize ?? (level == 1 ? 20.0 : (level == 2 ? 16.0 : 14.0));
        string fontWeight = level <= 2 ? " font-weight=\"bold\"" : string.Empty;
        double textY = node.Y + node.Height / 2 + fontSize * 0.35;
        sb.AppendLine($"""  <text x="{SvgRenderSupport.F(node.X)}" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}"{fontWeight} fill="{SvgRenderSupport.Escape(p.TextColor)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
    }

    // Text (body / bold)

    private static void AppendWfText(StringBuilder sb, Node node, Theme theme, WireframePalette p)
    {
        if (string.IsNullOrWhiteSpace(node.Label.Text))
            return;

        bool bold = node.Metadata.TryGetValue("wireframe:bold", out var bv) && bv is true;
        double fontSize = theme.FontSize;
        string fontWeightAttr = bold ? " font-weight=\"bold\"" : string.Empty;
        double textY = node.Y + node.Height / 2 + fontSize * 0.35;
        string color = bold ? p.TextColor : p.SubtleText;
        sb.AppendLine($"""  <text x="{SvgRenderSupport.F(node.X)}" y="{SvgRenderSupport.F(textY)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(fontSize)}"{fontWeightAttr} fill="{SvgRenderSupport.Escape(color)}">{SvgRenderSupport.Escape(node.Label.Text)}</text>""");
    }
}