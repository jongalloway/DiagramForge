using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

/// <summary>
/// Parses the Conceptual Diagram DSL into a unified <see cref="Diagram"/> model.
/// </summary>
/// <remarks>
/// <para>The DSL is a lightweight YAML-inspired text format. Example:</para>
/// <code>
/// diagram: matrix
/// rows:
///   - Product
///   - Engineering
/// columns:
///   - Now
///   - Next
/// </code>
/// <para>Supported diagram types: matrix, pyramid.</para>
/// </remarks>
public sealed class ConceptualDslParser : IDiagramParser
{
    public string SyntaxId => "conceptual";

    private static readonly HashSet<string> KnownTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "matrix", "pyramid",
    };

    /// <inheritdoc/>
    public bool CanParse(string diagramText)
    {
        if (string.IsNullOrWhiteSpace(diagramText))
            return false;

        var firstLine = diagramText.TrimStart().Split('\n')[0].Trim();
        if (!firstLine.StartsWith("diagram:", StringComparison.OrdinalIgnoreCase))
            return false;

        var typeValue = firstLine["diagram:".Length..].Trim().ToLowerInvariant();
        return KnownTypes.Contains(typeValue);
    }

    /// <inheritdoc/>
    public Diagram Parse(string diagramText)
    {
        if (string.IsNullOrWhiteSpace(diagramText))
            throw new DiagramParseException("Diagram text cannot be null or empty.");

        var lines = diagramText
            .Split('\n')
            .Select(l => l.TrimEnd())
            .ToArray();

        if (lines.Length == 0)
            throw new DiagramParseException("Diagram text is empty.");

        var header = lines[0].Trim();
        if (!header.StartsWith("diagram:", StringComparison.OrdinalIgnoreCase))
            throw new DiagramParseException("Conceptual DSL must begin with 'diagram: <type>'.");

        var diagramType = header["diagram:".Length..].Trim().ToLowerInvariant();

        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax(SyntaxId)
            .WithDiagramType(diagramType);

        switch (diagramType)
        {
            case "pyramid":
                ParseListDiagram(lines, builder, "levels", diagramType);
                break;
            case "matrix":
                ParseMatrixDiagram(lines, builder);
                break;
            default:
                throw new DiagramParseException($"Unknown conceptual diagram type: '{diagramType}'.");
        }

        return builder.Build();
    }

    // ── Parsers ───────────────────────────────────────────────────────────────

    private static void ParseListDiagram(
        string[] lines,
        IDiagramSemanticModelBuilder builder,
        string sectionKey,
        string diagramType)
    {
        int sectionLine = FindSectionLine(lines, sectionKey);
        if (sectionLine < 0)
            throw new DiagramParseException($"Missing required section '{sectionKey}:' in {diagramType} diagram.");

        var items = ReadListItems(lines, sectionLine + 1);
        if (items.Count == 0)
            throw new DiagramParseException($"Section '{sectionKey}' contains no items.");

        for (int i = 0; i < items.Count; i++)
        {
            var nodeId = $"node_{i}";
            builder.AddNode(new Node(nodeId, items[i]));
        }

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });
    }

    private static void ParseMatrixDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        // Expected format:
        //   rows:
        //     - Row A
        //     - Row B
        //   columns:
        //     - Col 1
        //     - Col 2

        int rowsLine = FindSectionLine(lines, "rows");
        int colsLine = FindSectionLine(lines, "columns");

        var rows = rowsLine >= 0 ? ReadListItems(lines, rowsLine + 1) : new List<string>();
        var cols = colsLine >= 0 ? ReadListItems(lines, colsLine + 1) : new List<string>();

        if (rows.Count == 0 || cols.Count == 0)
            throw new DiagramParseException("Matrix diagram requires non-empty 'rows' and 'columns' sections.");

        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < cols.Count; c++)
            {
                var nodeId = $"cell_{r}_{c}";
                var label = $"{rows[r]} / {cols[c]}";
                builder.AddNode(new Node(nodeId, label));
            }
        }

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static int FindSectionLine(string[] lines, string key)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Equals($"{key}:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static List<string> ReadListItems(string[] lines, int startIndex)
    {
        var items = new List<string>();
        for (int i = startIndex; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Stop when we hit the next top-level key (no leading spaces, ends with ':')
            if (!lines[i].StartsWith(' ') && !lines[i].StartsWith('\t') && trimmed.EndsWith(':'))
                break;

            if (trimmed.StartsWith('-'))
                items.Add(trimmed[1..].Trim());
        }
        return items;
    }
}
