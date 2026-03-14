using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutChevronDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        var orderedNodes = diagram.Nodes.Values
            .OrderBy(node => TryParseNodeIndex(node.Id))
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();

        if (orderedNodes.Count == 0)
            return;

        double widestLabel = orderedNodes.Max(node =>
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            return EstimateTextWidth(node.Label, fontSize) + 2 * theme.NodePadding;
        });

        // Arrow tip depth is proportional to the node height so it scales with font size.
        double tipDepth = nodeH * 0.35;

        // Each chevron's bounding-box width must accommodate both the label and the tip.
        double nodeW = Math.Max(widestLabel + tipDepth, minW);
        int stageCount = orderedNodes.Count;
        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;

        for (int index = 0; index < orderedNodes.Count; index++)
        {
            var node = orderedNodes[index];

            // Zero gap: bounding boxes abut so the tip of stage N meets the notch of stage N+1.
            node.X = pad + index * nodeW;
            node.Y = pad + titleOffset;
            node.Width = nodeW;
            node.Height = nodeH;

            node.Metadata["conceptual:chevronSegment"] = true;
            node.Metadata["conceptual:chevronIndex"] = index;
            node.Metadata["conceptual:chevronCount"] = stageCount;
            node.Metadata["conceptual:chevronTipDepth"] = tipDepth;

            // Center the label in the rectangular body of the chevron:
            // - First chevron: body from x=0 to x=(W-tip), visual center at (W-tip)/2
            // - Subsequent chevrons: body from x=tip to x=(W-tip), visual center at W/2
            node.Metadata["label:centerX"] = index == 0
                ? (nodeW - tipDepth) / 2.0
                : nodeW / 2.0;
        }
    }
}
