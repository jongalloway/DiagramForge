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
/// <para>Supported diagram types: matrix, pyramid, cycle, pillars, funnel, radial, tree.</para>
/// </remarks>
public sealed partial class ConceptualDslParser : IDiagramParser
{
    private readonly record struct IconLabeledText(string Label, string? IconRef);

    public string SyntaxId => "conceptual";

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

        if (!ParseHandlers.TryGetValue(diagramType, out var parse))
            throw new DiagramParseException($"Unknown conceptual diagram type: '{diagramType}'.");

        parse(lines, builder);

        return builder.Build();
    }

    private static void ParseListDiagram(
        string[] lines,
        IDiagramSemanticModelBuilder builder,
        string sectionKey,
        string diagramType,
        int minItems = 1)
    {
        int sectionLine = FindSectionLine(lines, sectionKey);
        if (sectionLine < 0)
            throw new DiagramParseException($"Missing required section '{sectionKey}:' in {diagramType} diagram.");

        var items = ReadListItems(lines, sectionLine + 1);
        if (items.Count == 0)
            throw new DiagramParseException($"Section '{sectionKey}' contains no items.");

        if (items.Count < minItems)
            throw new DiagramParseException(
                $"{diagramType} diagram requires at least {minItems} {sectionKey}, but {items.Count} was provided.");

        for (int i = 0; i < items.Count; i++)
        {
            var nodeId = $"node_{i}";
            var spec = ParseIconLabeledText(items[i]);
            builder.AddNode(new Node(nodeId, spec.Label) { IconRef = spec.IconRef });
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

    private static IconLabeledText ParseIconLabeledText(string rawText)
    {
        var (label, iconRef) = IconReferenceSyntax.Extract(rawText);
        return new IconLabeledText(label, iconRef);
    }

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
    private static List<(IconLabeledText Title, List<IconLabeledText> Segments)> ReadPillars(string[] lines, int startIndex)
    {
        var result = new List<(IconLabeledText Title, List<IconLabeledText> Segments)>();
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

            var title = ParseIconLabeledText(trimmed["- title:".Length..].Trim());
            var segments = new List<IconLabeledText>();
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
                            segments.Add(ParseIconLabeledText(segTrimmed[1..].Trim()));

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
        const int SpacesPerTab = 2;
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ') count++;
            else if (c == '\t') count += SpacesPerTab;
            else break;
        }
        return count;
    }
}
