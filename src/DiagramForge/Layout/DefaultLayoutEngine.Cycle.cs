using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutCycleDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        var orderedNodes = diagram.Nodes.Values
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .ToList();

        if (orderedNodes.Count == 0)
            return;

        double widestLabel = orderedNodes.Max(node =>
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            return EstimateTextWidth(node.Label.Text, fontSize) + 2 * theme.NodePadding;
        });

        double nodeW = Math.Max(widestLabel, minW);
        int n = orderedNodes.Count;

        double diagonal = Math.Sqrt(nodeW * nodeW + nodeH * nodeH);
        const double minNodeGap = 20.0;
        double minRadiusFromSpacing = (diagonal + minNodeGap) / (2 * Math.Sin(Math.PI / n));

        double minRadiusFloor = nodeW * 1.2 + pad;
        double radius = Math.Max(minRadiusFromSpacing, minRadiusFloor);

        double cx = pad + radius + nodeW / 2;
        double cy = pad + radius + nodeH / 2;

        for (int i = 0; i < n; i++)
        {
            var node = orderedNodes[i];
            double angle = -Math.PI / 2 + (2 * Math.PI * i / n);
            node.X = cx + radius * Math.Cos(angle) - nodeW / 2;
            node.Y = cy + radius * Math.Sin(angle) - nodeH / 2;
            node.Width = nodeW;
            node.Height = nodeH;
        }

        foreach (var edge in diagram.Edges)
        {
            edge.Metadata["conceptual:cycleArc"] = true;
            edge.Metadata["cycle:centerX"] = cx;
            edge.Metadata["cycle:centerY"] = cy;
            edge.Metadata["cycle:radius"] = radius;
        }
    }
}