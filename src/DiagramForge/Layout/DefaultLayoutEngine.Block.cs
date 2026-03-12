using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutBlockDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double hGap,
        double vGap,
        double pad)
    {
        foreach (var node in diagram.Nodes.Values)
        {
            bool isArrow = node.Shape is Shape.ArrowRight or Shape.ArrowLeft or Shape.ArrowUp or Shape.ArrowDown;
            SizeStandardNode(node, theme, isArrow ? minW * 0.75 : minW, isArrow ? nodeH + 8 : nodeH);
        }

        var placedNodes = diagram.Nodes.Values
            .Where(node => node.Metadata.ContainsKey("block:row") && node.Metadata.ContainsKey("block:column"))
            .ToList();

        if (placedNodes.Count == 0)
            return;

        int rowCount = placedNodes.Max(node => GetMetadataInt(node.Metadata, "block:row")) + 1;
        double baseColumnWidth = Math.Max(
            minW,
            placedNodes.Max(node =>
            {
                int span = TryGetMetadataInt(node.Metadata, "block:span", out var spanValue)
                    ? spanValue
                    : 1;
                return node.Width / Math.Max(1, span);
            }));

        var rowHeights = Enumerable.Repeat(nodeH, rowCount).ToArray();
        foreach (var node in placedNodes)
        {
            int row = GetMetadataInt(node.Metadata, "block:row");
            rowHeights[row] = Math.Max(rowHeights[row], node.Height);
        }

        var rowStarts = new double[rowCount];
        double currentY = pad;
        for (int row = 0; row < rowCount; row++)
        {
            rowStarts[row] = currentY;
            currentY += rowHeights[row] + vGap;
        }

        foreach (var node in placedNodes)
        {
            int row = GetMetadataInt(node.Metadata, "block:row");
            int column = GetMetadataInt(node.Metadata, "block:column");
            int span = TryGetMetadataInt(node.Metadata, "block:span", out var spanValue)
                ? spanValue
                : 1;

            double slotX = pad + column * (baseColumnWidth + hGap);
            double slotWidth = baseColumnWidth * span + hGap * Math.Max(0, span - 1);
            node.X = slotX;
            node.Y = rowStarts[row] + (rowHeights[row] - node.Height) / 2;
            node.Width = Math.Max(node.Width, slotWidth);
        }
    }
}