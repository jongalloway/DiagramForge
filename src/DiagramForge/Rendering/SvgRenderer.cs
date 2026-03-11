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
        sb.AppendLine($"""  <rect width="{F(width)}" height="{F(height)}" fill="{Escape(theme.BackgroundColor)}" rx="{F(theme.BorderRadius)}" ry="{F(theme.BorderRadius)}"/>""");

        // Title
        if (!string.IsNullOrWhiteSpace(diagram.Title))
        {
            sb.AppendLine($"""  <text x="{F(width / 2)}" y="{F(theme.DiagramPadding - 4)}" text-anchor="middle" font-family="{Escape(theme.FontFamily)}" font-size="{F(theme.TitleFontSize)}" font-weight="bold" fill="{Escape(theme.TextColor)}">{Escape(diagram.Title)}</text>""");
        }

        // Groups (render behind nodes)
        foreach (var group in diagram.Groups)
            AppendGroup(sb, group, theme);

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

        // Nodes
        foreach (var node in diagram.Nodes.Values)
            AppendNode(sb, node, theme);

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

    private static void AppendNode(StringBuilder sb, Node node, Theme theme)
    {
        string fill = Escape(node.FillColor ?? theme.NodeFillColor);
        string stroke = Escape(node.StrokeColor ?? theme.NodeStrokeColor);
        double rx = theme.BorderRadius;

        sb.AppendLine($"""  <g transform="translate({F(node.X)},{F(node.Y)})">""");

        switch (node.Shape)
        {
            case Shape.Circle:
            case Shape.Ellipse:
                double cx = node.Width / 2, cy = node.Height / 2;
                sb.AppendLine($"""    <ellipse cx="{F(cx)}" cy="{F(cy)}" rx="{F(cx)}" ry="{F(cy)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                break;

            case Shape.Diamond:
                double mx = node.Width / 2, my = node.Height / 2;
                sb.AppendLine($"""    <polygon points="{F(mx)},0 {F(node.Width)},{F(my)} {F(mx)},{F(node.Height)} 0,{F(my)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                break;

            case Shape.Pill:
            case Shape.Stadium:
                sb.AppendLine($"""    <rect width="{F(node.Width)}" height="{F(node.Height)}" rx="{F(node.Height / 2)}" ry="{F(node.Height / 2)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                break;

            case Shape.ArrowRight:
                AppendArrowPolygon(sb, node.Width, node.Height, fill, stroke, theme, "right");
                break;

            case Shape.ArrowLeft:
                AppendArrowPolygon(sb, node.Width, node.Height, fill, stroke, theme, "left");
                break;

            case Shape.ArrowUp:
                AppendArrowPolygon(sb, node.Width, node.Height, fill, stroke, theme, "up");
                break;

            case Shape.ArrowDown:
                AppendArrowPolygon(sb, node.Width, node.Height, fill, stroke, theme, "down");
                break;

            case Shape.Rectangle:
                sb.AppendLine($"""    <rect width="{F(node.Width)}" height="{F(node.Height)}" rx="0" ry="0" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                break;

            case Shape.Cloud:
                AppendCloudPath(sb, node.Width, node.Height, fill, stroke, theme);
                break;

            default: // RoundedRectangle and anything else
                sb.AppendLine($"""    <rect width="{F(node.Width)}" height="{F(node.Height)}" rx="{F(rx)}" ry="{F(rx)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                break;
        }

        // Label
        double textX = node.Width / 2;
        double textY = node.Height / 2 + theme.FontSize * 0.35;
        string textColor = Escape(node.Label.Color ?? theme.TextColor);
        double fontSize = node.Label.FontSize ?? theme.FontSize;

        sb.AppendLine($"""    <text x="{F(textX)}" y="{F(textY)}" text-anchor="middle" font-family="{Escape(theme.FontFamily)}" font-size="{F(fontSize)}" fill="{textColor}">{Escape(node.Label.Text)}</text>""");
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
        string strokeDash = edge.LineStyle == EdgeLineStyle.Dashed ? """ stroke-dasharray="6,3" """ :
                            edge.LineStyle == EdgeLineStyle.Dotted ? """ stroke-dasharray="2,3" """ : " ";
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

    private static void AppendGroup(StringBuilder sb, Group group, Theme theme)
    {
        string fill = Escape(group.FillColor ?? "#F3F4F6");
        string stroke = Escape(group.StrokeColor ?? "#D1D5DB");

        sb.AppendLine($"""  <rect x="{F(group.X)}" y="{F(group.Y)}" width="{F(group.Width)}" height="{F(group.Height)}" rx="{F(theme.BorderRadius)}" ry="{F(theme.BorderRadius)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}" opacity="0.6"/>""");

        if (!string.IsNullOrWhiteSpace(group.Label.Text))
        {
            sb.AppendLine($"""  <text x="{F(group.X + 8)}" y="{F(group.Y + theme.FontSize + 4)}" font-family="{Escape(theme.FontFamily)}" font-size="{F(theme.FontSize * 0.9)}" fill="{Escape(theme.SubtleTextColor)}" font-weight="bold">{Escape(group.Label.Text)}</text>""");
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

    // Palette for XY chart series (bars and lines cycle through these).
    private static readonly string[] SeriesPalette =
    [
        "#4F81BD", "#70AD47", "#ED7D31", "#FFC000", "#5B9BD5",
        "#A5A5A5", "#264478", "#9B57A0", "#636363", "#255E91",
    ];

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
                sb.AppendLine($"""  <line x1="{F(chartX)}" y1="{F(yPos)}" x2="{F(chartX + plotWidth)}" y2="{F(yPos)}" stroke="{axisColor}" stroke-width="0.5" opacity="0.2"/>""");

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
            string lineColor = Escape(SeriesPalette[(barSeriesCount + si) % SeriesPalette.Length]);

            sb.AppendLine($"""  <polyline points="{string.Join(" ", points)}" fill="none" stroke="{lineColor}" stroke-width="{F(theme.StrokeWidth * 1.5)}" stroke-linejoin="round" stroke-linecap="round"/>""");
        }
    }

    private static void AppendArrowPolygon(StringBuilder sb, double width, double height, string fill, string stroke, Theme theme, string direction)
    {
        double head = Math.Min(width, height) * 0.4;
        string points = direction switch
        {
            "left" => $"{F(width)},{F(height * 0.2)} {F(head)},{F(height * 0.2)} {F(head)},{F(0)} 0,{F(height / 2)} {F(head)},{F(height)} {F(head)},{F(height * 0.8)} {F(width)},{F(height * 0.8)}",
            "up" => $"{F(width * 0.2)},{F(height)} {F(width * 0.2)},{F(head)} 0,{F(head)} {F(width / 2)},0 {F(width)},{F(head)} {F(width * 0.8)},{F(head)} {F(width * 0.8)},{F(height)}",
            "down" => $"{F(width * 0.2)},0 {F(width * 0.2)},{F(height - head)} 0,{F(height - head)} {F(width / 2)},{F(height)} {F(width)},{F(height - head)} {F(width * 0.8)},{F(height - head)} {F(width * 0.8)},0",
            _ => $"0,{F(height * 0.2)} {F(width - head)},{F(height * 0.2)} {F(width - head)},0 {F(width)},{F(height / 2)} {F(width - head)},{F(height)} {F(width - head)},{F(height * 0.8)} 0,{F(height * 0.8)}",
        };

        sb.AppendLine($"""    <polygon points="{points}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
    }

    private static void AppendCloudPath(StringBuilder sb, double width, double height, string fill, string stroke, Theme theme)
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
        sb.AppendLine($"""    <path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
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

    private static string Escape(string? text) =>
        text is null ? string.Empty
            : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
