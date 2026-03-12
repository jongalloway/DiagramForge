using System.Collections.Frozen;
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
/// <para>Supported diagram types: matrix, pyramid, pillars.</para>
/// </remarks>
public sealed class ConceptualDslParser : IDiagramParser
{
    public string SyntaxId => "conceptual";

    private static readonly FrozenSet<string> KnownTypes = new[] { "matrix", "pyramid", "pillars" }
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool CanParse(string diagramText)
    {
        if (string.IsNullOrWhiteSpace(diagramText))
            return false;

        if (!TryReadFirstNonEmptyLine(diagramText.AsSpan(), out var firstLine))
            return false;

        if (!firstLine.StartsWith("diagram:", StringComparison.OrdinalIgnoreCase))
            return false;

        var typeValue = firstLine["diagram:".Length..].Trim().ToString();
        return KnownTypes.Contains(typeValue);
    }

    /// <inheritdoc/>
    public Diagram Parse(string diagramText)
    {
        if (string.IsNullOrWhiteSpace(diagramText))
            throw new DiagramParseException("Diagram text cannot be null or empty.");

        var lines = ReadLines(diagramText);

        if (lines.Length == 0)
            throw new DiagramParseException("Diagram text is empty.");

        var header = lines[0].Trim();
        if (!header.StartsWith("diagram:", StringComparison.OrdinalIgnoreCase))
            throw new DiagramParseException("Conceptual DSL must begin with 'diagram: <type>'.");

        var diagramType = header["diagram:".Length..].Trim().ToLowerInvariant();

        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax(SyntaxId)
            .WithDiagramType(diagramType);

        Action parse = diagramType switch
        {
            "pyramid" => () => ParseListDiagram(lines, builder, "levels", diagramType),
            "matrix" => () => ParseMatrixDiagram(lines, builder),
            "pillars" => () => ParsePillarsDiagram(lines, builder),
            _ => throw new DiagramParseException($"Unknown conceptual diagram type: '{diagramType}'."),
        };

        parse();

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

    private static void ParsePillarsDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        int pillarsLine = FindSectionLine(lines, "pillars");
        if (pillarsLine < 0)
            throw new DiagramParseException("Missing required section 'pillars:' in pillars diagram.");

        var pillars = ReadPillars(lines, pillarsLine + 1);

        if (pillars.Count == 0)
            throw new DiagramParseException("Section 'pillars' contains no pillar entries.");

        if (pillars.Count < 2 || pillars.Count > 5)
            throw new DiagramParseException("Pillars diagram requires between 2 and 5 pillars.");

        for (int i = 0; i < pillars.Count; i++)
        {
            var (title, segments) = pillars[i];

            var titleNode = new Node($"pillar_{i}", title);
            titleNode.Metadata["pillars:pillarIndex"] = i;
            titleNode.Metadata["pillars:kind"] = "title";
            builder.AddNode(titleNode);

            for (int j = 0; j < segments.Count; j++)
            {
                var segNode = new Node($"pillar_{i}_segment_{j}", segments[j]);
                segNode.Metadata["pillars:pillarIndex"] = i;
                segNode.Metadata["pillars:segmentIndex"] = j;
                segNode.Metadata["pillars:kind"] = "segment";
                builder.AddNode(segNode);
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
        => [.. EnumerateListItems(lines, startIndex)];

    private static string[] ReadLines(string text)
    {
        var lines = new List<string>();

        var span = text.AsSpan();
        int start = 0;
        while (start <= span.Length)
        {
            int length = 0;
            while (start + length < span.Length && span[start + length] != '\n')
                length++;

            lines.Add(TrimLineEnd(span.Slice(start, length)).ToString());

            start += length + 1;
            if (start > span.Length)
                break;
        }

        return [.. lines];
    }

    private static bool TryReadFirstNonEmptyLine(ReadOnlySpan<char> text, out ReadOnlySpan<char> line)
    {
        int start = 0;
        while (start <= text.Length)
        {
            int length = 0;
            while (start + length < text.Length && text[start + length] != '\n')
                length++;

            var candidate = text.Slice(start, length);
            if (!candidate.IsEmpty && candidate[^1] == '\r')
                candidate = candidate[..^1];

            var trimmed = candidate.Trim();
            if (!trimmed.IsEmpty)
            {
                line = trimmed;
                return true;
            }

            start += length + 1;
            if (start > text.Length)
                break;
        }

        line = default;
        return false;
    }

    private static ReadOnlySpan<char> TrimLineEnd(ReadOnlySpan<char> line)
    {
        int end = line.Length;
        while (end > 0 && char.IsWhiteSpace(line[end - 1]) && line[end - 1] != '\n' && line[end - 1] != '\r')
            end--;

        return line[..end];
    }

    private static IEnumerable<string> EnumerateListItems(string[] lines, int startIndex)
    {
        for (int i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Stop when we hit the next top-level key (no leading spaces, ends with ':')
            if (!line.StartsWith(' ') && !line.StartsWith('\t') && trimmed.EndsWith(':'))
                yield break;

            if (trimmed.StartsWith('-'))
                yield return trimmed[1..].Trim();
        }
    }

    /// <summary>
    /// Parses the nested pillar/segment structure from the lines starting at
    /// <paramref name="startIndex"/>. Each pillar begins with <c>- title: X</c>
    /// and optionally contains a <c>segments:</c> sub-list.
    /// </summary>
    private static List<(string Title, List<string> Segments)> ReadPillars(string[] lines, int startIndex)
    {
        var result = new List<(string Title, List<string> Segments)>();
        int pillarEntryIndent = -1;
        int i = startIndex;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed)) { i++; continue; }

            // Stop at a new zero-indent section header (e.g. "title:" or "options:")
            if (GetIndent(line) == 0 && !trimmed.StartsWith('-') && trimmed.EndsWith(':'))
                break;

            // Each pillar starts with "- title: <name>"
            if (!trimmed.StartsWith("- title:", StringComparison.OrdinalIgnoreCase)) { i++; continue; }

            int thisIndent = GetIndent(line);
            if (pillarEntryIndent < 0)
                pillarEntryIndent = thisIndent;

            // Only process entries at the same indent level as the first one
            if (thisIndent != pillarEntryIndent) { i++; continue; }

            string title = trimmed["- title:".Length..].Trim();
            var segments = new List<string>();
            i++;

            // Parse body of this pillar (lines indented deeper than the pillar marker)
            while (i < lines.Length)
            {
                var bodyLine = lines[i];
                var bodyTrimmed = bodyLine.Trim();

                if (string.IsNullOrEmpty(bodyTrimmed)) { i++; continue; }

                int bodyIndent = GetIndent(bodyLine);

                // A line at or above the pillar marker indent ends this pillar's body
                if (bodyIndent <= pillarEntryIndent)
                    break;

                if (bodyTrimmed.Equals("segments:", StringComparison.OrdinalIgnoreCase))
                {
                    int segSectionIndent = bodyIndent;
                    i++;

                    // Read segment items (lines deeper than "segments:")
                    while (i < lines.Length)
                    {
                        var segLine = lines[i];
                        var segTrimmed = segLine.Trim();

                        if (string.IsNullOrEmpty(segTrimmed)) { i++; continue; }

                        int segIndent = GetIndent(segLine);

                        // Returning to or above "segments:" indent ends the segment list
                        if (segIndent <= segSectionIndent)
                            break;

                        if (segTrimmed.StartsWith('-'))
                            segments.Add(segTrimmed[1..].Trim());

                        i++;
                    }

                    // i now sits at the first line at or above segSectionIndent;
                    // continue the body loop to re-evaluate that line
                }
                else
                {
                    i++; // Unknown pillar body content – skip
                }
            }

            result.Add((title, segments));
        }

        return result;
    }

    private static int GetIndent(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ') count++;
            else if (c == '\t') count += 2;
            else break;
        }
        return count;
    }
}
