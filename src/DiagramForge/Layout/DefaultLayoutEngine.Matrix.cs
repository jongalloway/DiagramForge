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

        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;
        double baseFontSize = cells.Max(node => node.Label.FontSize ?? theme.FontSize);
        int maxLineCount = cells.Max(node => GetTextLineCount(node.Label));
        double maxTextWidth = cells.Max(node => EstimateTextWidth(node.Label, node.Label.FontSize ?? theme.FontSize));

        double cellWidth = Math.Max(minW + 24, maxTextWidth + theme.NodePadding * 2.5);
        double textBlockHeight = Math.Max(1, maxLineCount) * baseFontSize * 1.15;
        double cellHeight = Math.Max(nodeH + baseFontSize * 0.7, textBlockHeight + theme.NodePadding * 2.6);
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