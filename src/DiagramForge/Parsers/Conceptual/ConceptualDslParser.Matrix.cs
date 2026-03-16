using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseMatrixDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        int rowsLine = FindSectionLine(lines, "rows");
        int colsLine = FindSectionLine(lines, "columns");
        int cellsLine = FindSectionLine(lines, "cells");

        var rows = rowsLine >= 0 ? ReadListItems(lines, rowsLine + 1) : [];
        var cols = colsLine >= 0 ? ReadListItems(lines, colsLine + 1) : [];
        var cells = cellsLine >= 0 ? ReadListItems(lines, cellsLine + 1) : [];

        if (rows.Count == 0 || cols.Count == 0)
            throw new DiagramParseException("Matrix diagram requires non-empty 'rows' and 'columns' sections.");

        if (rows.Count != 2 || cols.Count != 2)
            throw new DiagramParseException("Matrix diagram currently supports exactly 2 rows and 2 columns.");

        int maxCellCount = rows.Count * cols.Count;
        if (cells.Count > maxCellCount)
            throw new DiagramParseException($"Matrix diagram supports at most {maxCellCount} 'cells' entries, but {cells.Count} were provided.");

        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < cols.Count; c++)
            {
                var nodeId = $"cell_{r}_{c}";
                var node = new Node(nodeId, $"{cols[c]}\n{rows[r]}");
                int cellIndex = (r * cols.Count) + c;
                if (cellIndex < cells.Count)
                {
                    string cellValue = cells[cellIndex];
                    if (!string.IsNullOrWhiteSpace(cellValue))
                    {
                        var spec = ParseIconLabeledText(cellValue);
                        if (spec.IconRef is null)
                        {
                            throw new DiagramParseException(
                                $"Matrix 'cells' entry {cellIndex + 1} must be blank or contain an icon directive like 'icon:pack:name'.");
                        }

                        node.IconRef = spec.IconRef;
                    }
                }

                node.Metadata["matrix:row"] = r;
                node.Metadata["matrix:column"] = c;
                node.Metadata["matrix:rowLabel"] = rows[r];
                node.Metadata["matrix:columnLabel"] = cols[c];
                builder.AddNode(node);
            }
        }

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });
    }
}