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
        // Sort by the numeric suffix of the node ID ("node_0", "node_1", ...) so
        // that 10+ stages order correctly (string-ordinal would put node_10 between
        // node_1 and node_2).
        var orderedNodes = diagram.Nodes.Values
            .OrderBy(node => TryParseNodeIndex(node.Id))
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();

        if (orderedNodes.Count == 0)
            return;

        double widestLabel = orderedNodes.Max(node =>
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            return EnsureIconWidth(node, theme, EstimateTextWidth(node.Label, fontSize) + 2 * theme.NodePadding);
        });

        int stageCount = orderedNodes.Count;
        double nodeHeight = orderedNodes.Max(node => EnsureIconHeight(node, nodeH));
        double fullWidth = Math.Max(widestLabel, minW * 1.9);
        double titleOffset = ComputeHeadingOffset(diagram, theme);

        for (int index = 0; index < orderedNodes.Count; index++)
        {
            var node = orderedNodes[index];
            double yTop = index * (nodeHeight + FunnelLevelGap);

            // Compute widths from stage fraction, not from pixel Y position, so
            // that the gap between segments doesn't widen adjacent trapezoid edges.
            // Adjacent segments connect: bottom of segment i == top of segment i+1.
            double topWidth = fullWidth * (1 - (double)index / stageCount * (1 - FunnelMinWidthRatio));
            double segmentBottomWidth = fullWidth * (1 - (double)(index + 1) / stageCount * (1 - FunnelMinWidthRatio));

            node.X = pad;
            node.Y = pad + titleOffset + yTop;
            node.Width = fullWidth;
            node.Height = nodeHeight;
            node.Metadata["conceptual:funnelSegment"] = true;
            node.Metadata["conceptual:funnelTopWidth"] = topWidth;
            node.Metadata["conceptual:funnelBottomWidth"] = segmentBottomWidth;
        }
    }

    private static int TryParseNodeIndex(string nodeId)
    {
        int underscore = nodeId.LastIndexOf('_');
        if (underscore >= 0 && int.TryParse(nodeId.AsSpan(underscore + 1), out int idx))
            return idx;
        return int.MaxValue;
    }
}
