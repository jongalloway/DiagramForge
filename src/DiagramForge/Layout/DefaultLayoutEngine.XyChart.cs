using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutXyChartDiagram(
        Diagram diagram,
        Theme theme,
        double pad)
    {
        const double ChartWidth = 500;
        const double ChartHeight = 300;
        const double AxisLabelMarginLeft = 50;
        const double AxisLabelMarginBottom = 30;

        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;

        double chartX = pad + AxisLabelMarginLeft;
        double chartY = pad + titleOffset;
        double plotWidth = ChartWidth;
        double plotHeight = ChartHeight;

        int categoryCount = diagram.Metadata.TryGetValue("xychart:categoryCount", out var ccObj)
            ? Convert.ToInt32(ccObj, System.Globalization.CultureInfo.InvariantCulture) : 0;
        double yMin = diagram.Metadata.TryGetValue("xychart:yMin", out var yMinObj)
            ? Convert.ToDouble(yMinObj, System.Globalization.CultureInfo.InvariantCulture) : 0;
        double yMax = diagram.Metadata.TryGetValue("xychart:yMax", out var yMaxObj)
            ? Convert.ToDouble(yMaxObj, System.Globalization.CultureInfo.InvariantCulture) : 100;
        int barSeriesCount = diagram.Metadata.TryGetValue("xychart:barSeriesCount", out var bscObj)
            ? Convert.ToInt32(bscObj, System.Globalization.CultureInfo.InvariantCulture) : 0;

        double yRange = yMax - yMin;
        if (yRange <= 0)
            yRange = 1;

        double categoryWidth = categoryCount > 0 ? plotWidth / categoryCount : plotWidth;
        double barGroupWidth = categoryWidth * 0.7;
        double barWidth = barSeriesCount > 0 ? barGroupWidth / barSeriesCount : barGroupWidth;
        double barOffsetInCategory = (categoryWidth - barGroupWidth) / 2;

        diagram.Metadata["xychart:chartX"] = chartX;
        diagram.Metadata["xychart:chartY"] = chartY;
        diagram.Metadata["xychart:plotWidth"] = plotWidth;
        diagram.Metadata["xychart:plotHeight"] = plotHeight;

        foreach (var node in diagram.Nodes.Values)
        {
            if (!node.Metadata.TryGetValue("xychart:kind", out var kindObj))
                continue;

            var kind = kindObj as string;
            int ci = node.Metadata.TryGetValue("xychart:categoryIndex", out var ciObj)
                ? Convert.ToInt32(ciObj, System.Globalization.CultureInfo.InvariantCulture) : 0;
            double value = node.Metadata.TryGetValue("xychart:value", out var valObj)
                ? Convert.ToDouble(valObj, System.Globalization.CultureInfo.InvariantCulture) : 0;

            double clampedValue = Math.Max(yMin, Math.Min(yMax, value));
            double normalized = (clampedValue - yMin) / yRange;
            double barHeight = normalized * plotHeight;

            if (kind == "bar")
            {
                int si = node.Metadata.TryGetValue("xychart:seriesIndex", out var siObj)
                    ? Convert.ToInt32(siObj, System.Globalization.CultureInfo.InvariantCulture) : 0;

                node.X = chartX + ci * categoryWidth + barOffsetInCategory + si * barWidth;
                node.Y = chartY + plotHeight - barHeight;
                node.Width = barWidth;
                node.Height = barHeight;
            }
            else if (kind == "linePoint")
            {
                double pointX = chartX + ci * categoryWidth + categoryWidth / 2;
                double pointY = chartY + plotHeight - normalized * plotHeight;
                node.X = pointX - 3;
                node.Y = pointY - 3;
                node.Width = 6;
                node.Height = 6;
            }
        }

        double canvasWidth = chartX + plotWidth + pad;
        double canvasHeight = chartY + plotHeight + AxisLabelMarginBottom + pad;
        diagram.Metadata["xychart:canvasWidth"] = canvasWidth;
        diagram.Metadata["xychart:canvasHeight"] = canvasHeight;
    }
}