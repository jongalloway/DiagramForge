using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutRadialDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        if (!diagram.Nodes.TryGetValue("center", out var centerNode))
            return;

        var itemNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.ContainsKey("radial:itemIndex"))
            .OrderBy(n => GetMetadataInt(n.Metadata, "radial:itemIndex"))
            .ToList();

        if (itemNodes.Count == 0)
            return;

        int n = itemNodes.Count;

        double fontSize = theme.FontSize;
        double itemNodeW = Math.Max(minW,
            itemNodes.Max(node => EstimateTextWidth(node.Label.Text, node.Label.FontSize ?? fontSize) + theme.NodePadding * 2));
        double itemNodeH = nodeH;

        double centerLabelW = EstimateTextWidth(centerNode.Label.Text, centerNode.Label.FontSize ?? fontSize);
        double centerDiameter = Math.Max(minW * 1.5, centerLabelW + theme.NodePadding * 3);

        // Radius: large enough so item nodes don't overlap and leave a gap from the center.
        double itemDiagonal = Math.Sqrt(itemNodeW * itemNodeW + itemNodeH * itemNodeH);
        const double minItemGap = 20.0;
        double minRadiusFromSpacing = (itemDiagonal + minItemGap) / (2 * Math.Sin(Math.PI / n));
        double minRadiusFromCenter = centerDiameter / 2 + itemNodeW / 2 + 40.0;
        double radius = Math.Max(minRadiusFromSpacing, minRadiusFromCenter);

        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;

        // Canvas center point
        double cx = pad + radius + Math.Max(itemNodeW, centerDiameter) / 2;
        double cy = pad + titleOffset + radius + Math.Max(itemNodeH, centerDiameter) / 2;

        // Place center node as a circle
        centerNode.Shape = Shape.Circle;
        centerNode.Width = centerDiameter;
        centerNode.Height = centerDiameter;
        centerNode.X = cx - centerDiameter / 2;
        centerNode.Y = cy - centerDiameter / 2;
        SetLabelCenter(centerNode, centerDiameter / 2, centerDiameter / 2);

        // Color center node with the first palette entry (or default fill)
        string[] palette = theme.NodePalette is { Count: > 0 }
            ? [.. theme.NodePalette]
            : [theme.NodeFillColor];

        centerNode.FillColor = palette[0];
        centerNode.StrokeColor = theme.NodeStrokePalette is { Count: > 0 }
            ? theme.NodeStrokePalette[0]
            : ColorUtils.Darken(palette[0], 0.20);

        // Place item nodes evenly around the circle, starting at top (-π/2)
        for (int i = 0; i < n; i++)
        {
            var node = itemNodes[i];
            double angle = -Math.PI / 2 + (2 * Math.PI * i / n);
            node.X = cx + radius * Math.Cos(angle) - itemNodeW / 2;
            node.Y = cy + radius * Math.Sin(angle) - itemNodeH / 2;
            node.Width = itemNodeW;
            node.Height = itemNodeH;

            // Cycle through remaining palette entries for items
            string itemFill = palette[(i + 1) % palette.Length];
            node.FillColor = itemFill;
            node.StrokeColor = theme.NodeStrokePalette is { Count: > 0 }
                ? theme.NodeStrokePalette[(i + 1) % theme.NodeStrokePalette.Count]
                : ColorUtils.Darken(itemFill, 0.20);

            SetLabelCenter(node, itemNodeW / 2, itemNodeH / 2);
        }
    }
}
