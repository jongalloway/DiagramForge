using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseMatrixDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        int rowsLine = FindSectionLine(lines, "rows");
        int colsLine = FindSectionLine(lines, "columns");

        var rows = rowsLine >= 0 ? ReadListItems(lines, rowsLine + 1) : [];
        var cols = colsLine >= 0 ? ReadListItems(lines, colsLine + 1) : [];

        if (rows.Count == 0 || cols.Count == 0)
            throw new DiagramParseException("Matrix diagram requires non-empty 'rows' and 'columns' sections.");

        if (rows.Count != 2 || cols.Count != 2)
            throw new DiagramParseException("Matrix diagram currently supports exactly 2 rows and 2 columns.");

        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < cols.Count; c++)
            {
                var nodeId = $"cell_{r}_{c}";
                var node = new Node(nodeId, $"{cols[c]}\n{rows[r]}");
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