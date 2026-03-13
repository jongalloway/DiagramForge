using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    // The narrowest segment (bottom of funnel) as a fraction of the widest (top).
    private const double FunnelMinWidthRatio = 0.20;
    private const double FunnelLevelGap = 6;

    private static void LayoutFunnelDiagram(
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
            return EstimateTextWidth(node.Label, fontSize) + 2 * theme.NodePadding;
        });

        int stageCount = orderedNodes.Count;
        double fullWidth = Math.Max(widestLabel, minW * 1.9);
        double totalHeight = stageCount * nodeH + (stageCount - 1) * FunnelLevelGap;

        for (int index = 0; index < orderedNodes.Count; index++)
        {
            var node = orderedNodes[index];
            double yTop = index * (nodeH + FunnelLevelGap);
            double yBottom = yTop + nodeH;

            // Linear taper: width decreases from fullWidth at the top edge to
            // fullWidth * FunnelMinWidthRatio at the bottom edge of the last segment.
            double topWidth = fullWidth * (1 - yTop / totalHeight * (1 - FunnelMinWidthRatio));
            double segmentBottomWidth = fullWidth * (1 - yBottom / totalHeight * (1 - FunnelMinWidthRatio));

            node.X = pad;
            node.Y = pad + yTop;
            node.Width = fullWidth;
            node.Height = nodeH;
            node.Metadata["conceptual:funnelSegment"] = true;
            node.Metadata["conceptual:funnelTopWidth"] = topWidth;
            node.Metadata["conceptual:funnelBottomWidth"] = segmentBottomWidth;
        }
    }
}
