using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutPyramidDiagram(
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

        int levelCount = orderedNodes.Count;
        double bottomWidth = Math.Max(widestLabel, minW * 1.9);
        double totalHeight = levelCount * nodeH + (levelCount - 1) * PyramidLevelGap;

        for (int index = 0; index < orderedNodes.Count; index++)
        {
            var node = orderedNodes[index];
            double yTop = index * (nodeH + PyramidLevelGap);
            double yBottom = yTop + nodeH;
            double topWidth = bottomWidth * yTop / totalHeight;
            double segmentBottomWidth = bottomWidth * yBottom / totalHeight;

            node.X = pad;
            node.Y = pad + yTop;
            node.Width = bottomWidth;
            node.Height = nodeH;
            node.Metadata["conceptual:pyramidSegment"] = true;
            node.Metadata["conceptual:pyramidTopWidth"] = topWidth;
            node.Metadata["conceptual:pyramidBottomWidth"] = segmentBottomWidth;
        }
    }
}