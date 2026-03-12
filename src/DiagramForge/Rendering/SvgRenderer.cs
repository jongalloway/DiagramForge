using System.Text;
using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Rendering;

/// <summary>
/// Renders a laid-out <see cref="Diagram"/> to a self-contained SVG string.
/// </summary>
/// <remarks>
/// The renderer produces clean, modern SVG with rounded corners, theme-driven colours,
/// and smooth edge paths. No external libraries or browser runtime is required.
/// </remarks>
public sealed class SvgRenderer : ISvgRenderer
{
    private const double AvgGlyphAdvanceEm = 0.6;

    /// <inheritdoc/>
    public string Render(Diagram diagram, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(theme);

        double width = ComputeWidth(diagram, theme);
        double height = ComputeHeight(diagram, theme);

        var sb = new StringBuilder();

        // SVG root — use F() for all numeric attributes to guarantee invariant-culture output
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{F(width)}" height="{F(height)}" viewBox="0 0 {F(width)} {F(height)}">""");

        // Definitions (arrow markers, etc.)
        AppendDefs(sb, theme);

        // Background
        if (!theme.TransparentBackground)
        {
            sb.AppendLine($"""  <rect width="{F(width)}" height="{F(height)}" fill="{Escape(theme.BackgroundColor)}" rx="{F(theme.BorderRadius)}" ry="{F(theme.BorderRadius)}"/>""");
        }

        // Title
        if (!string.IsNullOrWhiteSpace(diagram.Title))
        {
            sb.AppendLine($"""  <text x="{F(width / 2)}" y="{F(theme.DiagramPadding - 4)}" text-anchor="middle" font-family="{Escape(theme.FontFamily)}" font-size="{F(theme.TitleFontSize)}" font-weight="bold" fill="{Escape(theme.TitleTextColor)}">{Escape(diagram.Title)}</text>""");
        }

        // Groups (render behind nodes)
        int groupIndex = 0;
        foreach (var group in diagram.Groups)
            AppendGroup(sb, group, theme, groupIndex++);

        // Sequence-diagram lifelines: dashed vertical lines below each participant box.
        if (diagram.Metadata.ContainsKey("sequence:canvasHeight"))
            AppendLifelines(sb, diagram, theme, height);

        // XY chart axes, grid lines, and line series.
        if (diagram.Metadata.ContainsKey("xychart:chartX"))
            AppendXyChartAxes(sb, diagram, theme);

        // Edges (render behind nodes)
        foreach (var edge in diagram.Edges)
        {
            if (!diagram.Nodes.TryGetValue(edge.SourceId, out var source)
                || !diagram.Nodes.TryGetValue(edge.TargetId, out var target))
                continue;

            AppendEdge(sb, edge, source, target, theme);
        }

        // Nodes (pass index for palette cycling)
        int nodeIndex = 0;
        foreach (var node in diagram.Nodes.Values)
            AppendNode(sb, node, theme, nodeIndex++);

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    // ── SVG building blocks ───────────────────────────────────────────────────

    private static void AppendDefs(StringBuilder sb, Theme theme)
    {
        sb.AppendLine("  <defs>");
        sb.AppendLine($"""    <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">""");
        sb.AppendLine($"""      <polygon points="0 0, 10 3.5, 0 7" fill="{Escape(theme.EdgeColor)}"/>""");
        sb.AppendLine("    </marker>");
        sb.AppendLine("  </defs>");
    }

    private static void AppendNode(StringBuilder sb, Node node, Theme theme, int nodeIndex = 0)
    {
        string? xyChartKind = node.Metadata.TryGetValue("xychart:kind", out var xyKindObj) ? xyKindObj as string : null;
        string baseFill;
        string baseStroke;

        if (TryResolveXyChartColors(node, theme, out string chartFill, out string chartStroke))
        {
            baseFill = chartFill;
            baseStroke = chartStroke;
        }
        else if (node.FillColor is not null)
        {
            // Explicit per-node color takes priority
            baseFill = node.FillColor;
            baseStroke = node.StrokeColor ?? ColorUtils.Darken(node.FillColor, 0.20);
        }
        else if (theme.NodePalette is { Count: > 0 })
        {
            // Cycle through the palette
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
        double? fillOpacity = GetMetadataDouble(node, "render:fillOpacity");
        if (!fillOpacity.HasValue && string.Equals(xyChartKind, "bar", StringComparison.OrdinalIgnoreCase))
        {
            fillOpacity = ColorUtils.IsLight(theme.BackgroundColor) ? 0.88 : 0.80;
        }
        string fillOpacityAttribute = fillOpacity.HasValue
            ? $" fill-opacity=\"{F(fillOpacity.Value)}\""
            : string.Empty;
        double rx = theme.BorderRadius;
        bool textOnly = node.Metadata.TryGetValue("render:textOnly", out var textOnlyObj)
            && textOnlyObj is bool isTextOnly
            && isTextOnly;
        bool applyNodeShadow = theme.UseNodeShadows
            && string.Equals(theme.ShadowStyle, "soft", StringComparison.OrdinalIgnoreCase)
            && !textOnly;

        sb.AppendLine($"""  <g transform="translate({F(node.X)},{F(node.Y)})">""");

        AppendGradientDefs(sb, "    ", $"node-{nodeIndex}", baseFill, baseStroke, theme, out string fill, out string stroke);
        AppendShadowFilterDefs(sb, "    ", $"node-{nodeIndex}", theme, out string? nodeShadowFilterId, applyNodeShadow);
        string nodeShadowAttribute = nodeShadowFilterId is null ? string.Empty : $" filter=\"url(#{nodeShadowFilterId})\"";

        if (!textOnly)
        {
            if (node.Metadata.TryGetValue("conceptual:pyramidSegment", out var pyramidSegmentObj)
                && pyramidSegmentObj is bool isPyramidSegment
                && isPyramidSegment)
            {
                AppendPyramidSegmentNode(sb, node, fill, stroke, theme, fillOpacityAttribute, nodeShadowAttribute);
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
                        AppendRoundedRectNode(sb, node, fill, stroke, theme, fillOpacityAttribute, GetXyChartBarRadius(node, theme), nodeShadowAttribute);
                    else
                        AppendRoundedRectNode(sb, node, fill, stroke, theme, fillOpacityAttribute, 0, nodeShadowAttribute);
                    break;
                case Shape.Cloud:
                    AppendCloudPath(sb, node.Width, node.Height, fill, stroke, theme, nodeShadowAttribute);
                    break;
                default:
                    AppendRoundedRectNode(sb, node, fill, stroke, theme, fillOpacityAttribute, rx, nodeShadowAttribute);
                    break;
            }
            }
        }
        else if (HasTextOnlyBackdrop(node, fillOpacity))
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            double textWidth = EstimateTextWidth(node.Label.Text, fontSize);
            double horizontalPadding = theme.NodePadding * 0.4;
            double top = -fontSize * 0.80;
            double height = fontSize * 1.25;
            double width = textWidth + horizontalPadding * 2;
            sb.AppendLine($"""    <rect x="{F(-width / 2)}" y="{F(top)}" width="{F(width)}" height="{F(height)}" rx="{F(fontSize * 0.35)}" ry="{F(fontSize * 0.35)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"{fillOpacityAttribute}/>""");
        }

        // Label
        if (!string.IsNullOrWhiteSpace(node.Label.Text))
        {
            string resolvedTextColor = node.Label.Color ?? ResolveNodeTextColor(baseFill, theme);
            string textColor = Escape(resolvedTextColor);
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            double textX = GetMetadataDouble(node, "label:centerX") ?? (textOnly ? 0 : (node.Width / 2));
            double textBaselineY = GetMetadataDouble(node, "label:centerY") ?? (textOnly ? 0 : (node.Height / 2));

            AppendNodeLabel(sb, node.Label.Text, theme, textX, textBaselineY, fontSize, textColor);
        }
        sb.AppendLine("  </g>");
    }

    private static void AppendEdge(StringBuilder sb, Edge edge, Node source, Node target, Theme theme)
    {
        // Compute anchor points based on the dominant direction between the two nodes
        // so that horizontal layouts connect right→left and vertical layouts connect bottom→top.
        double sourceCenterX = source.X + source.Width / 2;
        double sourceCenterY = source.Y + source.Height / 2;
        double targetCenterX = target.X + target.Width / 2;
        double targetCenterY = target.Y + target.Height / 2;

        double dx = targetCenterX - sourceCenterX;
        double dy = targetCenterY - sourceCenterY;

        double x1, y1, x2, y2;
        string cp1, cp2;

        // Determine whether horizontal anchor points would overshoot — i.e.
        // the source's connecting edge is past the target's connecting edge.
        // This happens when nodes overlap horizontally (e.g., a wide Storage bar
        // connecting to a narrower Backend above it): the "right-to-left" anchors
        // would create a looping path.
        bool horizontalOverlap = (dx >= 0 && source.X + source.Width > target.X)
                              || (dx < 0 && source.X < target.X + target.Width);
        bool verticalOverlap = (dy >= 0 && source.Y + source.Height > target.Y)
                             || (dy < 0 && source.Y < target.Y + target.Height);

        bool preferHorizontal = Math.Abs(dx) >= Math.Abs(dy);
        // If the preferred axis overshoots, try the other axis; if both
        // overshoot, keep the original preference (degenerate/overlapping case).
        if (preferHorizontal && horizontalOverlap && !verticalOverlap)
            preferHorizontal = false;
        else if (!preferHorizontal && verticalOverlap && !horizontalOverlap)
            preferHorizontal = true;

        if (preferHorizontal)
        {
            // Predominantly horizontal: connect right side → left side (or left → right for reversed)
            if (dx >= 0)
            {
                x1 = source.X + source.Width;
                y1 = sourceCenterY;
                x2 = target.X;
                y2 = targetCenterY;
            }
            else
            {
                x1 = source.X;
                y1 = sourceCenterY;
                x2 = target.X + target.Width;
                y2 = targetCenterY;
            }
        }
        else
        {
            // Predominantly vertical: connect bottom-center → top-center
            if (dy >= 0)
            {
                x1 = sourceCenterX;
                y1 = source.Y + source.Height;
                x2 = targetCenterX;
                y2 = target.Y;
            }
            else
            {
                x1 = sourceCenterX;
                y1 = source.Y;
                x2 = targetCenterX;
                y2 = target.Y + target.Height;
            }
        }

        // Build control points:
        //  cp1 — axis-aligned from the source anchor so the path departs with a
        //         smooth curve perpendicular to the node edge.
        //  cp2 — along the actual source→target direction so that the curve's
        //         tangent at the endpoint (and therefore the orient="auto"
        //         arrowhead) matches the visual angle of approach.
        // For axis-aligned edges both strategies coincide, so curves and arrows
        // look the same as before.
        double edgeDx = x2 - x1;
        double edgeDy = y2 - y1;
        double edgeLen = Math.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
        double cpDist = edgeLen * 0.4;
        if (edgeLen > 0)
        {
            double ux = edgeDx / edgeLen;
            double uy = edgeDy / edgeLen;

            // cp1: axis-aligned departure (horizontal or vertical depending on
            // which node edge the anchor sits on).
            if (preferHorizontal)
                cp1 = $"{F(x1 + (dx >= 0 ? cpDist : -cpDist))},{F(y1)}";
            else
                cp1 = $"{F(x1)},{F(y1 + (dy >= 0 ? cpDist : -cpDist))}";

            // cp2: follows the real edge vector so the arrowhead angles correctly.
            cp2 = $"{F(x2 - ux * cpDist)},{F(y2 - uy * cpDist)}";
        }
        else
        {
            cp1 = $"{F(x1)},{F(y1)}";
            cp2 = $"{F(x2)},{F(y2)}";
        }

        // Per-message Y override: sequence diagrams store an explicit Y position for each
        // message arrow so that multiple messages between the same participants are stacked
        // vertically rather than all drawn on top of each other at the node center.
        if (edge.Metadata.TryGetValue("sequence:messageY", out var msgYObj))
        {
            double msgY = Convert.ToDouble(msgYObj, System.Globalization.CultureInfo.InvariantCulture);
            x1 = sourceCenterX;
            y1 = msgY;
            x2 = targetCenterX;
            y2 = msgY;
            double seqOffset = Math.Abs(x2 - x1) * 0.4;
            cp1 = $"{F(x1 + (x2 >= x1 ? seqOffset : -seqOffset))},{F(y1)}";
            cp2 = $"{F(x2 - (x2 >= x1 ? seqOffset : -seqOffset))},{F(y2)}";
        }

        string strokeColor = Escape(edge.Color ?? theme.EdgeColor);
        string strokeDash = edge.LineStyle switch
        {
            EdgeLineStyle.Dashed => """ stroke-dasharray="6,3" """,
            EdgeLineStyle.Dotted => """ stroke-dasharray="2,3" """,
            _ => " ",
        };
        double strokeWidth = edge.LineStyle == EdgeLineStyle.Thick ? theme.StrokeWidth * 2 : theme.StrokeWidth;
        string markerEnd = edge.ArrowHead != ArrowHeadStyle.None ? """ marker-end="url(#arrowhead)" """ : " ";

        sb.AppendLine($"""  <path d="M {F(x1)},{F(y1)} C {cp1} {cp2} {F(x2)},{F(y2)}" fill="none" stroke="{strokeColor}" stroke-width="{F(strokeWidth)}"{strokeDash}{markerEnd}/>""");

        // Edge label
        if (edge.Label is not null && !string.IsNullOrWhiteSpace(edge.Label.Text))
        {
            double lx = (x1 + x2) / 2;
            double ly = (y1 + y2) / 2 - 4;
            sb.AppendLine($"""  <text x="{F(lx)}" y="{F(ly)}" text-anchor="middle" font-family="{Escape(theme.FontFamily)}" font-size="{F(theme.FontSize * 0.85)}" fill="{Escape(theme.SubtleTextColor)}" font-style="italic">{Escape(edge.Label.Text)}</text>""");
        }
    }

    private static void AppendGroup(StringBuilder sb, Group group, Theme theme, int groupIndex)
    {
        string baseFill = group.FillColor ?? theme.GroupFillColor;
        string baseStroke = group.StrokeColor ?? theme.GroupStrokeColor;
        AppendGradientDefs(sb, "  ", $"group-{groupIndex}", baseFill, baseStroke, theme, out string fill, out string stroke);
        AppendShadowFilterDefs(sb, "  ", $"group-{groupIndex}", theme, out string? shadowFilterId);

        string shadowAttribute = shadowFilterId is null ? string.Empty : $" filter=\"url(#{shadowFilterId})\"";

        sb.AppendLine($"""  <rect x="{F(group.X)}" y="{F(group.Y)}" width="{F(group.Width)}" height="{F(group.Height)}" rx="{F(theme.BorderRadius)}" ry="{F(theme.BorderRadius)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"{shadowAttribute}/>""");

        if (!string.IsNullOrWhiteSpace(group.Label.Text))
        {
            double badgeFontSize = theme.FontSize * 0.82;
            double badgeWidth = EstimateTextWidth(group.Label.Text, badgeFontSize) + 18;
            double badgeHeight = badgeFontSize + 10;
            double badgeX = group.X + 10;
            double badgeY = group.Y + 10;
            string badgeFill = Escape(ColorUtils.Blend(theme.BackgroundColor, baseStroke, ColorUtils.IsLight(theme.BackgroundColor) ? 0.10 : 0.22));
            string badgeStroke = Escape(ColorUtils.Blend(baseStroke, theme.BackgroundColor, ColorUtils.IsLight(theme.BackgroundColor) ? 0.18 : 0.08));
            string badgeText = Escape(ResolveNodeTextColor(ColorUtils.Blend(baseFill, theme.BackgroundColor, 0.35), theme));

            sb.AppendLine($"""  <rect x="{F(badgeX)}" y="{F(badgeY)}" width="{F(badgeWidth)}" height="{F(badgeHeight)}" rx="{F(badgeHeight / 2)}" ry="{F(badgeHeight / 2)}" fill="{badgeFill}" stroke="{badgeStroke}" stroke-width="{F(theme.StrokeWidth * 0.8)}"/>""");
            sb.AppendLine($"""  <text x="{F(badgeX + 9)}" y="{F(badgeY + badgeHeight * 0.68)}" font-family="{Escape(theme.FontFamily)}" font-size="{F(badgeFontSize)}" fill="{badgeText}" font-weight="bold">{Escape(group.Label.Text)}</text>""");
        }
    }

    private static void AppendLifelines(StringBuilder sb, Diagram diagram, Theme theme, double canvasHeight)
    {
        string stroke = Escape(theme.EdgeColor);
        double bottomY = canvasHeight - theme.DiagramPadding;

        foreach (var node in diagram.Nodes.Values)
        {
            double cx = node.X + node.Width / 2;
            double topY = node.Y + node.Height;
            sb.AppendLine($"""  <line x1="{F(cx)}" y1="{F(topY)}" x2="{F(cx)}" y2="{F(bottomY)}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}" stroke-dasharray="6,3"/>""");
        }
    }

    private static void AppendXyChartAxes(StringBuilder sb, Diagram diagram, Theme theme)
    {
        double chartX = Convert.ToDouble(diagram.Metadata["xychart:chartX"], System.Globalization.CultureInfo.InvariantCulture);
        double chartY = Convert.ToDouble(diagram.Metadata["xychart:chartY"], System.Globalization.CultureInfo.InvariantCulture);
        double plotWidth = Convert.ToDouble(diagram.Metadata["xychart:plotWidth"], System.Globalization.CultureInfo.InvariantCulture);
        double plotHeight = Convert.ToDouble(diagram.Metadata["xychart:plotHeight"], System.Globalization.CultureInfo.InvariantCulture);
        double yMin = Convert.ToDouble(diagram.Metadata["xychart:yMin"], System.Globalization.CultureInfo.InvariantCulture);
        double yMax = Convert.ToDouble(diagram.Metadata["xychart:yMax"], System.Globalization.CultureInfo.InvariantCulture);
        int categoryCount = Convert.ToInt32(diagram.Metadata["xychart:categoryCount"], System.Globalization.CultureInfo.InvariantCulture);
        var categories = diagram.Metadata["xychart:categories"] as string[] ?? [];
        int lineSeriesCount = diagram.Metadata.TryGetValue("xychart:lineSeriesCount", out var lscObj)
            ? Convert.ToInt32(lscObj, System.Globalization.CultureInfo.InvariantCulture) : 0;

        string axisColor = Escape(theme.EdgeColor);
        string textColor = Escape(theme.SubtleTextColor);
        double fontSize = theme.FontSize * 0.85;
        string fontFamily = Escape(theme.FontFamily);
        double categoryWidth = categoryCount > 0 ? plotWidth / categoryCount : plotWidth;

        // Y-axis line
        sb.AppendLine($"""  <line x1="{F(chartX)}" y1="{F(chartY)}" x2="{F(chartX)}" y2="{F(chartY + plotHeight)}" stroke="{axisColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");

        // X-axis line
        sb.AppendLine($"""  <line x1="{F(chartX)}" y1="{F(chartY + plotHeight)}" x2="{F(chartX + plotWidth)}" y2="{F(chartY + plotHeight)}" stroke="{axisColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");

        // Y-axis ticks and labels (5 evenly spaced)
        int yTickCount = 5;
        double yRange = yMax - yMin;
        for (int t = 0; t <= yTickCount; t++)
        {
            double frac = (double)t / yTickCount;
            double yPos = chartY + plotHeight - frac * plotHeight;
            double yVal = yMin + frac * yRange;
            string label = yVal.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

            // Tick mark
            sb.AppendLine($"""  <line x1="{F(chartX - 4)}" y1="{F(yPos)}" x2="{F(chartX)}" y2="{F(yPos)}" stroke="{axisColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");

            // Grid line (light)
            if (t > 0 && t < yTickCount)
                sb.AppendLine($"""  <line x1="{F(chartX)}" y1="{F(yPos)}" x2="{F(chartX + plotWidth)}" y2="{F(yPos)}" stroke="{axisColor}" stroke-width="0.8" opacity="0.55" stroke-dasharray="2,6" stroke-linecap="round"/>""");

            // Label
            sb.AppendLine($"""  <text x="{F(chartX - 8)}" y="{F(yPos + fontSize * 0.35)}" text-anchor="end" font-family="{fontFamily}" font-size="{F(fontSize)}" fill="{textColor}">{Escape(label)}</text>""");
        }

        // X-axis category labels
        for (int ci = 0; ci < categoryCount && ci < categories.Length; ci++)
        {
            double labelX = chartX + ci * categoryWidth + categoryWidth / 2;
            double labelY = chartY + plotHeight + fontSize + 4;
            sb.AppendLine($"""  <text x="{F(labelX)}" y="{F(labelY)}" text-anchor="middle" font-family="{fontFamily}" font-size="{F(fontSize)}" fill="{textColor}">{Escape(categories[ci])}</text>""");
        }

        // Y-axis label (rotated)
        if (diagram.Metadata.TryGetValue("xychart:yLabel", out var yLabelObj) && yLabelObj is string yLabel)
        {
            double labelX = chartX - 40;
            double labelY = chartY + plotHeight / 2;
            sb.AppendLine($"""  <text x="{F(labelX)}" y="{F(labelY)}" text-anchor="middle" font-family="{fontFamily}" font-size="{F(fontSize)}" fill="{textColor}" transform="rotate(-90,{F(labelX)},{F(labelY)})">{Escape(yLabel)}</text>""");
        }

        // Line series polylines
        for (int si = 0; si < lineSeriesCount; si++)
        {
            var points = diagram.Nodes.Values
                .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "linePoint"
                         && n.Metadata.TryGetValue("xychart:seriesIndex", out var siObj)
                         && Convert.ToInt32(siObj, System.Globalization.CultureInfo.InvariantCulture) == si)
                .OrderBy(n => Convert.ToInt32(n.Metadata["xychart:categoryIndex"], System.Globalization.CultureInfo.InvariantCulture))
                .Select(n => $"{F(n.X + n.Width / 2)},{F(n.Y + n.Height / 2)}")
                .ToList();

            if (points.Count < 2)
                continue;

            // Bar series get lower palette indices; line series start after them.
            int barSeriesCount = diagram.Metadata.TryGetValue("xychart:barSeriesCount", out var bscObj)
                ? Convert.ToInt32(bscObj, System.Globalization.CultureInfo.InvariantCulture) : 0;
            string lineColor = Escape(GetXyChartSeriesColor(theme, barSeriesCount + si));

            sb.AppendLine($"""  <polyline points="{string.Join(" ", points)}" fill="none" stroke="{lineColor}" stroke-width="{F(theme.StrokeWidth * 1.5)}" stroke-linejoin="round" stroke-linecap="round"/>""");
        }
    }

    private static void AppendArrowPolygon(StringBuilder sb, double width, double height, string fill, string stroke, Theme theme, string direction, string shadowAttribute)
    {
        double head = Math.Min(width, height) * 0.4;
        string points = direction switch
        {
            "left" => $"{F(width)},{F(height * 0.2)} {F(head)},{F(height * 0.2)} {F(head)},{F(0)} 0,{F(height / 2)} {F(head)},{F(height)} {F(head)},{F(height * 0.8)} {F(width)},{F(height * 0.8)}",
            "up" => $"{F(width * 0.2)},{F(height)} {F(width * 0.2)},{F(head)} 0,{F(head)} {F(width / 2)},0 {F(width)},{F(head)} {F(width * 0.8)},{F(head)} {F(width * 0.8)},{F(height)}",
            "down" => $"{F(width * 0.2)},0 {F(width * 0.2)},{F(height - head)} 0,{F(height - head)} {F(width / 2)},{F(height)} {F(width)},{F(height - head)} {F(width * 0.8)},{F(height - head)} {F(width * 0.8)},0",
            _ => $"0,{F(height * 0.2)} {F(width - head)},{F(height * 0.2)} {F(width - head)},0 {F(width)},{F(height / 2)} {F(width - head)},{F(height)} {F(width - head)},{F(height * 0.8)} 0,{F(height * 0.8)}",
        };

        sb.AppendLine($"""    <polygon points="{points}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"{shadowAttribute}/>""");
    }

    private static void AppendEllipseNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string fillOpacityAttribute, string shadowAttribute)
    {
        double cx = node.Width / 2;
        double cy = node.Height / 2;
        sb.AppendLine($"""    <ellipse cx="{F(cx)}" cy="{F(cy)}" rx="{F(cx)}" ry="{F(cy)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"{fillOpacityAttribute}{shadowAttribute}/>""");
    }

    private static void AppendDiamondNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string fillOpacityAttribute, string shadowAttribute)
    {
        double mx = node.Width / 2;
        double my = node.Height / 2;
        sb.AppendLine($"""    <polygon points="{F(mx)},0 {F(node.Width)},{F(my)} {F(mx)},{F(node.Height)} 0,{F(my)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"{fillOpacityAttribute}{shadowAttribute}/>""");
    }

    private static void AppendPyramidSegmentNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string fillOpacityAttribute, string shadowAttribute)
    {
        double topWidth = GetMetadataDouble(node, "conceptual:pyramidTopWidth") ?? 0;
        double bottomWidth = GetMetadataDouble(node, "conceptual:pyramidBottomWidth") ?? node.Width;
        double topInset = (node.Width - topWidth) / 2;
        double bottomInset = (node.Width - bottomWidth) / 2;

        string points = topWidth <= 0.01
            ? $"{F(node.Width / 2)},0 {F(bottomInset + bottomWidth)},{F(node.Height)} {F(bottomInset)},{F(node.Height)}"
            : $"{F(topInset)},0 {F(topInset + topWidth)},0 {F(bottomInset + bottomWidth)},{F(node.Height)} {F(bottomInset)},{F(node.Height)}";

        sb.AppendLine($"""    <polygon points="{points}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"{fillOpacityAttribute}{shadowAttribute}/>""");
    }

    private static void AppendRoundedRectNode(StringBuilder sb, Node node, string fill, string stroke, Theme theme, string fillOpacityAttribute, double radius, string shadowAttribute)
    {
        string formattedRadius = radius == 0 ? "0" : F(radius);
        sb.AppendLine($"""    <rect width="{F(node.Width)}" height="{F(node.Height)}" rx="{formattedRadius}" ry="{formattedRadius}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"{fillOpacityAttribute}{shadowAttribute}/>""");
    }

    private static void AppendCloudPath(StringBuilder sb, double width, double height, string fill, string stroke, Theme theme, string shadowAttribute)
    {
        // Approximate a cloud shape using a path with arc segments.
        // The cloud is built from 5 overlapping arcs across the top and a flat bottom.
        double w = width;
        double h = height;
        double r1 = h * 0.28; // large left bump
        double r2 = h * 0.22; // small left bump
        double r3 = h * 0.30; // large top bump
        double r4 = h * 0.24; // small right bump
        double r5 = h * 0.20; // small far-right bump
        double flatBottomY = h * 0.72; // y-level of flat bottom

        // Approximate arc path: start bottom-left, trace arc bumps across the top, back to bottom-right, then flat bottom.
        string d =
            $"M {F(w * 0.10)},{F(flatBottomY)} " +
            $"A {F(r1)},{F(r1)} 0 0,1 {F(w * 0.20)},{F(flatBottomY - r1 * 1.1)} " +
            $"A {F(r2)},{F(r2)} 0 0,1 {F(w * 0.37)},{F(flatBottomY - r2 * 1.5)} " +
            $"A {F(r3)},{F(r3)} 0 0,1 {F(w * 0.60)},{F(flatBottomY - r3 * 1.0)} " +
            $"A {F(r4)},{F(r4)} 0 0,1 {F(w * 0.80)},{F(flatBottomY - r4 * 0.9)} " +
            $"A {F(r5)},{F(r5)} 0 0,1 {F(w * 0.92)},{F(flatBottomY)} " +
            $"Z";
        sb.AppendLine($"""    <path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"{shadowAttribute}/>""");
    }

    // ── Dimension helpers ─────────────────────────────────────────────────────

    private static double ComputeWidth(Diagram diagram, Theme theme)
    {
        if (diagram.Nodes.Count == 0)
            return 200;

        if (diagram.Metadata.TryGetValue("xychart:canvasWidth", out var xcW))
            return Convert.ToDouble(xcW, System.Globalization.CultureInfo.InvariantCulture);

        double maxX = diagram.Nodes.Values.Max(n => n.X + n.Width);
        // Group rects extend beyond their member nodes by their own padding;
        // without this, the group's right edge is clipped at the canvas boundary.
        if (diagram.Groups.Count > 0)
            maxX = Math.Max(maxX, diagram.Groups.Max(g => g.X + g.Width));
        return maxX + theme.DiagramPadding;
    }

    private static double ComputeHeight(Diagram diagram, Theme theme)
    {
        if (diagram.Nodes.Count == 0)
            return 100;

        // Sequence diagrams extend below the participant nodes with per-message rows;
        // the layout engine stores the required height so node extents don't clip messages.
        if (diagram.Metadata.TryGetValue("sequence:canvasHeight", out var seqH))
            return Convert.ToDouble(seqH, System.Globalization.CultureInfo.InvariantCulture);

        if (diagram.Metadata.TryGetValue("xychart:canvasHeight", out var xcH))
            return Convert.ToDouble(xcH, System.Globalization.CultureInfo.InvariantCulture);

        double maxY = diagram.Nodes.Values.Max(n => n.Y + n.Height);
        if (diagram.Groups.Count > 0)
            maxY = Math.Max(maxY, diagram.Groups.Max(g => g.Y + g.Height));
        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;
        return maxY + theme.DiagramPadding + titleOffset;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string F(double v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    private static double EstimateTextWidth(string? text, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return text
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n')
            .Max(line => line.Length) * fontSize * AvgGlyphAdvanceEm;
    }

    private static void AppendNodeLabel(
        StringBuilder sb,
        string labelText,
        Theme theme,
        double centerX,
        double centerY,
        double fontSize,
        string textColor)
    {
        var lines = labelText.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        double lineHeight = fontSize * 1.15;
        double firstBaselineY = centerY + fontSize * 0.35 - ((lines.Length - 1) * lineHeight / 2);

        if (lines.Length == 1)
        {
            sb.AppendLine($"""    <text x="{F(centerX)}" y="{F(firstBaselineY)}" text-anchor="middle" font-family="{Escape(theme.FontFamily)}" font-size="{F(fontSize)}" fill="{textColor}">{Escape(lines[0])}</text>""");
            return;
        }

        sb.AppendLine($"""    <text x="{F(centerX)}" y="{F(firstBaselineY)}" text-anchor="middle" font-family="{Escape(theme.FontFamily)}" font-size="{F(fontSize)}" fill="{textColor}">""");
        sb.AppendLine($"""      <tspan x="{F(centerX)}" y="{F(firstBaselineY)}">{Escape(lines[0])}</tspan>""");

        for (int i = 1; i < lines.Length; i++)
        {
            sb.AppendLine($"""      <tspan x="{F(centerX)}" dy="{F(lineHeight)}">{Escape(lines[i])}</tspan>""");
        }

        sb.AppendLine("    </text>");
    }

    private static bool TryResolveXyChartColors(Node node, Theme theme, out string fillColor, out string strokeColor)
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

    private static string GetXyChartSeriesColor(Theme theme, int seriesIndex)
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

    private static string GetXyChartSeriesStrokeColor(string fillColor, Theme theme) =>
        ColorUtils.IsLight(theme.BackgroundColor)
            ? ColorUtils.Darken(fillColor, 0.12)
            : ColorUtils.Lighten(fillColor, 0.10);

    private static double GetXyChartBarRadius(Node node, Theme theme)
    {
        double maxRadius = Math.Min(Math.Min(node.Width, node.Height) * 0.18, Math.Max(4, theme.BorderRadius));
        return Math.Max(0, maxRadius);
    }

    private static bool HasTextOnlyBackdrop(Node node, double? fillOpacity) =>
        !string.IsNullOrWhiteSpace(node.Label.Text)
        && (node.FillColor is not null || node.StrokeColor is not null || fillOpacity.HasValue);

    private static string ResolveNodeTextColor(string fillColor, Theme theme)
    {
        string darkText = ColorUtils.IsLight(theme.TextColor) ? "#0F172A" : theme.TextColor;
        string lightText = ColorUtils.IsLight(theme.TextColor) ? theme.TextColor : "#F8FAFC";
        return ColorUtils.ChooseTextColor(fillColor, lightText, darkText);
    }

    private static void AppendGradientDefs(
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

    private static void AppendShadowFilterDefs(
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
        sb.AppendLine($"{indent}    <feDropShadow dx=\"{F(theme.ShadowOffsetX)}\" dy=\"{F(theme.ShadowOffsetY)}\" stdDeviation=\"{F(theme.ShadowBlur)}\" flood-color=\"{Escape(theme.ShadowColor)}\" flood-opacity=\"{F(theme.ShadowOpacity)}\"/>");
        sb.AppendLine($"{indent}  </filter>");
        sb.AppendLine($"{indent}</defs>");
    }

    private static double? GetMetadataDouble(Node node, string key)
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

    private static string Escape(string? text) =>
        text is null ? string.Empty
            : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
