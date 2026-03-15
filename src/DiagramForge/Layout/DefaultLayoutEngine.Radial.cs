using DiagramForge.Models;
using DiagramForge.Rendering;

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
        double itemNodeW = itemNodes.Max(node =>
            EnsureIconWidth(node, theme, Math.Max(minW, EstimateTextWidth(node.Label.Text, node.Label.FontSize ?? fontSize) + theme.NodePadding * 2)));
        double itemNodeH = itemNodes.Max(node => EnsureIconHeight(node, nodeH));

        double centerLabelW = EstimateTextWidth(centerNode.Label.Text, centerNode.Label.FontSize ?? fontSize);
        double centerDiameter = Math.Max(minW * 1.5, EnsureIconWidth(centerNode, theme, centerLabelW + theme.NodePadding * 3));
        centerDiameter = EnsureIconHeight(centerNode, centerDiameter);
        double centerIconSize = Math.Min(88, Math.Max(SvgNodeWriter.DefaultIconSize, centerDiameter * 0.34));
        if (centerNode.ResolvedIcon is not null)
        {
            double centerScale = centerIconSize / SvgNodeWriter.DefaultIconSize;
            centerNode.Label.FontSize = Math.Round((centerNode.Label.FontSize ?? fontSize) * centerScale, 2);
            PrepareLabelLines(centerNode.Label, theme, diagram.LayoutHints);

            centerLabelW = EstimateTextWidth(centerNode.Label.Text, centerNode.Label.FontSize ?? fontSize);
            centerDiameter = Math.Max(minW * 1.5, EnsureIconWidth(centerNode, theme, centerLabelW + theme.NodePadding * 3.2));
            centerDiameter = EnsureIconHeight(centerNode, centerDiameter);
            centerDiameter = Math.Max(centerDiameter, centerIconSize + theme.NodePadding * 3.5);
        }

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
        if (centerNode.ResolvedIcon is not null)
        {
            centerNode.Metadata["icon:size"] = centerIconSize;
            centerNode.Metadata["icon:y"] = Math.Max(theme.NodePadding, centerDiameter * 0.18);
        }

        // Color center node with the first palette entry (or default fill)
        string[] palette = theme.NodePalette is { Count: > 0 }
            ? [.. theme.NodePalette]
            : [theme.NodeFillColor];

        centerNode.FillColor = palette[0];
        centerNode.StrokeColor = theme.NodeStrokePalette is { Count: > 0 }
            ? theme.NodeStrokePalette[0]
            : ColorUtils.Darken(palette[0], 0.20);

        // Build an item-only sub-palette that never wraps back to the center color.
        // When the palette has more than one entry, skip index 0 (used by the center)
        // and cycle over the remaining entries. When the palette is a single color,
        // all items share that same color (same as the center — unavoidable).
        string[] itemPalette = palette.Length > 1 ? palette[1..] : palette;
        string[]? strokePalette = theme.NodeStrokePalette is { Count: > 1 }
            ? [.. theme.NodeStrokePalette.Skip(1)]
            : theme.NodeStrokePalette is { Count: 1 }
                ? [.. theme.NodeStrokePalette]
                : null;

        // Place item nodes evenly around the circle, starting at top (-π/2)
        for (int i = 0; i < n; i++)
        {
            var node = itemNodes[i];
            double angle = -Math.PI / 2 + (2 * Math.PI * i / n);
            node.X = cx + radius * Math.Cos(angle) - itemNodeW / 2;
            node.Y = cy + radius * Math.Sin(angle) - itemNodeH / 2;
            node.Width = itemNodeW;
            node.Height = itemNodeH;

            string itemFill = itemPalette[i % itemPalette.Length];
            node.FillColor = itemFill;
            node.StrokeColor = strokePalette is not null
                ? strokePalette[i % strokePalette.Length]
                : ColorUtils.Darken(itemFill, 0.20);

            SetLabelCenter(node, itemNodeW / 2, itemNodeH / 2);
        }
    }
}
