using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutMatrixDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        var cells = diagram.Nodes.Values
            .Where(node => node.Metadata.ContainsKey("matrix:row") && node.Metadata.ContainsKey("matrix:column"))
            .OrderBy(node => GetMetadataInt(node.Metadata, "matrix:row"))
            .ThenBy(node => GetMetadataInt(node.Metadata, "matrix:column"))
            .ToList();

        if (cells.Count == 0)
            return;

        double titleOffset = ComputeHeadingOffset(diagram, theme);
        double baseFontSize = cells.Max(node => node.Label.FontSize ?? theme.FontSize);
        double cellWidth = cells.Max(node =>
            EnsureIconWidth(node, theme, Math.Max(minW + 24, EstimateTextWidth(node.Label, node.Label.FontSize ?? theme.FontSize) + theme.NodePadding * 2.5)));
        double cellHeight = cells.Max(node =>
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            int lineCount = Math.Max(1, GetTextLineCount(node.Label));
            double textBlockHeight = lineCount * fontSize * 1.15;
            double baseHeight = Math.Max(nodeH + baseFontSize * 0.7, textBlockHeight + theme.NodePadding * 2.6);
            return EnsureIconHeight(node, baseHeight);
        });
        double gap = Math.Max(theme.NodePadding, 18);

        string[] palette = theme.NodePalette is { Count: > 0 }
            ? [.. theme.NodePalette]
            : [theme.NodeFillColor, theme.NodeFillColor, theme.NodeFillColor, theme.NodeFillColor];

        foreach (var cell in cells)
        {
            int row = GetMetadataInt(cell.Metadata, "matrix:row");
            int column = GetMetadataInt(cell.Metadata, "matrix:column");
            int paletteIndex = Math.Clamp(row * 2 + column, 0, palette.Length - 1);
            string fill = palette[paletteIndex];

            cell.Shape = Shape.RoundedRectangle;
            cell.Width = cellWidth;
            cell.Height = cellHeight;
            cell.X = pad + column * (cellWidth + gap);
            cell.Y = pad + titleOffset + row * (cellHeight + gap);
            cell.FillColor = fill;
            cell.StrokeColor = theme.NodeStrokePalette is { Count: > 0 }
                ? theme.NodeStrokePalette[paletteIndex % theme.NodeStrokePalette.Count]
                : ColorUtils.Darken(fill, 0.18);
            SetLabelCenter(cell, cellWidth / 2, cellHeight / 2);
        }
    }
}