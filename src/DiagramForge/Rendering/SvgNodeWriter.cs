using System.Text;
using DiagramForge.Models;

namespace DiagramForge.Rendering;

internal static partial class SvgNodeWriter
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
        else if (node.Metadata.TryGetValue("render:disableFillGradient", out var disableFillGradient) && disableFillGradient is true)
        {
            // Fill is always "none" for this node type (e.g. target ring circles), so skip the
            // fill-gradient def to avoid emitting unused SVG. Border gradient is still applied.
            fill = SvgRenderSupport.Escape(baseFill);
            SvgRenderSupport.AppendBorderGradientDef(sb, "    ", $"node-{nodeIndex}-stroke-gradient", baseStroke, theme, out stroke);
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