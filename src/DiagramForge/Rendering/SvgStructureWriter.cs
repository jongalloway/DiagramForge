using System.Text;
using DiagramForge.Models;

namespace DiagramForge.Rendering;

internal static class SvgStructureWriter
{
    internal static void AppendEdge(StringBuilder sb, Edge edge, Node source, Node target, Theme theme)
    {
        double sourceCenterX = source.X + source.Width / 2;
        double sourceCenterY = source.Y + source.Height / 2;
        double targetCenterX = target.X + target.Width / 2;
        double targetCenterY = target.Y + target.Height / 2;

        double dx = targetCenterX - sourceCenterX;
        double dy = targetCenterY - sourceCenterY;

        double x1;
        double y1;
        double x2;
        double y2;
        string cp1;
        string cp2;

        bool horizontalOverlap = (dx >= 0 && source.X + source.Width > target.X)
                              || (dx < 0 && source.X < target.X + target.Width);
        bool verticalOverlap = (dy >= 0 && source.Y + source.Height > target.Y)
                             || (dy < 0 && source.Y < target.Y + target.Height);

        bool preferHorizontal = Math.Abs(dx) >= Math.Abs(dy);
        if (preferHorizontal && horizontalOverlap && !verticalOverlap)
            preferHorizontal = false;
        else if (!preferHorizontal && verticalOverlap && !horizontalOverlap)
            preferHorizontal = true;

        if (preferHorizontal)
        {
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
        else if (dy >= 0)
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

        double edgeDx = x2 - x1;
        double edgeDy = y2 - y1;
        double edgeLen = Math.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
        double cpDist = edgeLen * 0.4;
        if (edgeLen > 0)
        {
            double ux = edgeDx / edgeLen;
            double uy = edgeDy / edgeLen;

            if (preferHorizontal)
                cp1 = $"{SvgRenderSupport.F(x1 + (dx >= 0 ? cpDist : -cpDist))},{SvgRenderSupport.F(y1)}";
            else
                cp1 = $"{SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1 + (dy >= 0 ? cpDist : -cpDist))}";

            cp2 = $"{SvgRenderSupport.F(x2 - ux * cpDist)},{SvgRenderSupport.F(y2 - uy * cpDist)}";
        }
        else
        {
            cp1 = $"{SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)}";
            cp2 = $"{SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}";
        }

        if (edge.Metadata.TryGetValue("sequence:messageY", out var msgYObj))
        {
            double msgY = Convert.ToDouble(msgYObj, System.Globalization.CultureInfo.InvariantCulture);
            x1 = sourceCenterX;
            y1 = msgY;
            x2 = targetCenterX;
            y2 = msgY;
            double seqOffset = Math.Abs(x2 - x1) * 0.4;
            cp1 = $"{SvgRenderSupport.F(x1 + (x2 >= x1 ? seqOffset : -seqOffset))},{SvgRenderSupport.F(y1)}";
            cp2 = $"{SvgRenderSupport.F(x2 - (x2 >= x1 ? seqOffset : -seqOffset))},{SvgRenderSupport.F(y2)}";
        }

        string strokeColor = SvgRenderSupport.Escape(edge.Color ?? theme.EdgeColor);
        string strokeDash = edge.LineStyle switch
        {
            EdgeLineStyle.Dashed => """ stroke-dasharray="6,3" """,
            EdgeLineStyle.Dotted => """ stroke-dasharray="2,3" """,
            _ => " ",
        };
        double strokeWidth = edge.LineStyle == EdgeLineStyle.Thick ? theme.StrokeWidth * 2 : theme.StrokeWidth;
        string markerEnd = edge.ArrowHead != ArrowHeadStyle.None ? """ marker-end="url(#arrowhead)" """ : " ";

        sb.AppendLine($"""  <path d="M {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)} C {cp1} {cp2} {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}" fill="none" stroke="{strokeColor}" stroke-width="{SvgRenderSupport.F(strokeWidth)}"{strokeDash}{markerEnd}/>""");

        if (edge.Label is not null && !string.IsNullOrWhiteSpace(edge.Label.Text))
        {
            double lx = (x1 + x2) / 2;
            double ly = (y1 + y2) / 2 - 4;
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(lx)}" y="{SvgRenderSupport.F(ly)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(theme.FontSize * 0.85)}" fill="{SvgRenderSupport.Escape(theme.SubtleTextColor)}" font-style="italic">{SvgRenderSupport.Escape(edge.Label.Text)}</text>""");
        }
    }

    internal static void AppendGroup(StringBuilder sb, Group group, Theme theme, int groupIndex)
    {
        string baseFill = group.FillColor ?? theme.GroupFillColor;
        string baseStroke = group.StrokeColor ?? theme.GroupStrokeColor;
        SvgRenderSupport.AppendGradientDefs(sb, "  ", $"group-{groupIndex}", baseFill, baseStroke, theme, out string fill, out string stroke);
        SvgRenderSupport.AppendShadowFilterDefs(sb, "  ", $"group-{groupIndex}", theme, out string? shadowFilterId);

        string shadowAttribute = shadowFilterId is null ? string.Empty : $" filter=\"url(#{shadowFilterId})\"";

        sb.AppendLine($"""  <rect x="{SvgRenderSupport.F(group.X)}" y="{SvgRenderSupport.F(group.Y)}" width="{SvgRenderSupport.F(group.Width)}" height="{SvgRenderSupport.F(group.Height)}" rx="{SvgRenderSupport.F(theme.BorderRadius)}" ry="{SvgRenderSupport.F(theme.BorderRadius)}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"{shadowAttribute}/>""");

        if (!string.IsNullOrWhiteSpace(group.Label.Text))
        {
            double badgeFontSize = theme.FontSize * 0.82;
            double badgeWidth = SvgRenderSupport.EstimateTextWidth(group.Label.Text, badgeFontSize) + 18;
            double badgeHeight = badgeFontSize + 10;
            double badgeX = group.X + 10;
            double badgeY = group.Y + 10;
            string badgeFill = SvgRenderSupport.Escape(ColorUtils.Blend(theme.BackgroundColor, baseStroke, ColorUtils.IsLight(theme.BackgroundColor) ? 0.10 : 0.22));
            string badgeStroke = SvgRenderSupport.Escape(ColorUtils.Blend(baseStroke, theme.BackgroundColor, ColorUtils.IsLight(theme.BackgroundColor) ? 0.18 : 0.08));
            string badgeText = SvgRenderSupport.Escape(SvgRenderSupport.ResolveNodeTextColor(ColorUtils.Blend(baseFill, theme.BackgroundColor, 0.35), theme));

            sb.AppendLine($"""  <rect x="{SvgRenderSupport.F(badgeX)}" y="{SvgRenderSupport.F(badgeY)}" width="{SvgRenderSupport.F(badgeWidth)}" height="{SvgRenderSupport.F(badgeHeight)}" rx="{SvgRenderSupport.F(badgeHeight / 2)}" ry="{SvgRenderSupport.F(badgeHeight / 2)}" fill="{badgeFill}" stroke="{badgeStroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth * 0.8)}"/>""");
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(badgeX + 9)}" y="{SvgRenderSupport.F(badgeY + badgeHeight * 0.68)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(badgeFontSize)}" fill="{badgeText}" font-weight="bold">{SvgRenderSupport.Escape(group.Label.Text)}</text>""");
        }
    }

    internal static void AppendLifelines(StringBuilder sb, Diagram diagram, Theme theme, double canvasHeight)
    {
        string stroke = SvgRenderSupport.Escape(theme.EdgeColor);
        double bottomY = canvasHeight - theme.DiagramPadding;

        foreach (var node in diagram.Nodes.Values)
        {
            double cx = node.X + node.Width / 2;
            double topY = node.Y + node.Height;
            sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(cx)}" y1="{SvgRenderSupport.F(topY)}" x2="{SvgRenderSupport.F(cx)}" y2="{SvgRenderSupport.F(bottomY)}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}" stroke-dasharray="6,3"/>""");
        }
    }

    internal static void AppendXyChartAxes(StringBuilder sb, Diagram diagram, Theme theme)
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

        string axisColor = SvgRenderSupport.Escape(theme.EdgeColor);
        string textColor = SvgRenderSupport.Escape(theme.SubtleTextColor);
        double fontSize = theme.FontSize * 0.85;
        string fontFamily = SvgRenderSupport.Escape(theme.FontFamily);
        double categoryWidth = categoryCount > 0 ? plotWidth / categoryCount : plotWidth;

        sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(chartX)}" y1="{SvgRenderSupport.F(chartY)}" x2="{SvgRenderSupport.F(chartX)}" y2="{SvgRenderSupport.F(chartY + plotHeight)}" stroke="{axisColor}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"/>""");
        sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(chartX)}" y1="{SvgRenderSupport.F(chartY + plotHeight)}" x2="{SvgRenderSupport.F(chartX + plotWidth)}" y2="{SvgRenderSupport.F(chartY + plotHeight)}" stroke="{axisColor}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"/>""");

        int yTickCount = 5;
        double yRange = yMax - yMin;
        for (int t = 0; t <= yTickCount; t++)
        {
            double frac = (double)t / yTickCount;
            double yPos = chartY + plotHeight - frac * plotHeight;
            double yVal = yMin + frac * yRange;
            string label = yVal.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

            sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(chartX - 4)}" y1="{SvgRenderSupport.F(yPos)}" x2="{SvgRenderSupport.F(chartX)}" y2="{SvgRenderSupport.F(yPos)}" stroke="{axisColor}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"/>""");

            if (t > 0 && t < yTickCount)
                sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(chartX)}" y1="{SvgRenderSupport.F(yPos)}" x2="{SvgRenderSupport.F(chartX + plotWidth)}" y2="{SvgRenderSupport.F(yPos)}" stroke="{axisColor}" stroke-width="0.8" opacity="0.55" stroke-dasharray="2,6" stroke-linecap="round"/>""");

            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(chartX - 8)}" y="{SvgRenderSupport.F(yPos + fontSize * 0.35)}" text-anchor="end" font-family="{fontFamily}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{textColor}">{SvgRenderSupport.Escape(label)}</text>""");
        }

        for (int ci = 0; ci < categoryCount && ci < categories.Length; ci++)
        {
            double labelX = chartX + ci * categoryWidth + categoryWidth / 2;
            double labelY = chartY + plotHeight + fontSize + 4;
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(labelX)}" y="{SvgRenderSupport.F(labelY)}" text-anchor="middle" font-family="{fontFamily}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{textColor}">{SvgRenderSupport.Escape(categories[ci])}</text>""");
        }

        if (diagram.Metadata.TryGetValue("xychart:yLabel", out var yLabelObj) && yLabelObj is string yLabel)
        {
            double labelX = chartX - 40;
            double labelY = chartY + plotHeight / 2;
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(labelX)}" y="{SvgRenderSupport.F(labelY)}" text-anchor="middle" font-family="{fontFamily}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{textColor}" transform="rotate(-90,{SvgRenderSupport.F(labelX)},{SvgRenderSupport.F(labelY)})">{SvgRenderSupport.Escape(yLabel)}</text>""");
        }

        for (int si = 0; si < lineSeriesCount; si++)
        {
            var points = diagram.Nodes.Values
                .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "linePoint"
                         && n.Metadata.TryGetValue("xychart:seriesIndex", out var siObj)
                         && Convert.ToInt32(siObj, System.Globalization.CultureInfo.InvariantCulture) == si)
                .OrderBy(n => Convert.ToInt32(n.Metadata["xychart:categoryIndex"], System.Globalization.CultureInfo.InvariantCulture))
                .Select(n => $"{SvgRenderSupport.F(n.X + n.Width / 2)},{SvgRenderSupport.F(n.Y + n.Height / 2)}")
                .ToList();

            if (points.Count < 2)
                continue;

            int barSeriesCount = diagram.Metadata.TryGetValue("xychart:barSeriesCount", out var bscObj)
                ? Convert.ToInt32(bscObj, System.Globalization.CultureInfo.InvariantCulture) : 0;
            string lineColor = SvgRenderSupport.Escape(SvgRenderSupport.GetXyChartSeriesColor(theme, barSeriesCount + si));

            sb.AppendLine($"""  <polyline points="{string.Join(" ", points)}" fill="none" stroke="{lineColor}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth * 1.5)}" stroke-linejoin="round" stroke-linecap="round"/>""");
        }
    }
}