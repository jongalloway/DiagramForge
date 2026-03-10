using DiagramForge.Abstractions;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidDocument
{
    private MermaidDocument(MermaidDiagramKind kind, string headerLine, string[] lines)
    {
        Kind = kind;
        HeaderLine = headerLine;
        Lines = lines;
    }

    public MermaidDiagramKind Kind { get; }

    public string HeaderLine { get; }

    public string[] Lines { get; }

    public static bool TryParse(string diagramText, out MermaidDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(diagramText))
            return false;

        var lines = diagramText
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("%%", StringComparison.Ordinal))
            .ToArray();

        if (lines.Length == 0)
            return false;

        var headerLine = lines[0];
        if (!TryDetectKind(headerLine, out var kind))
            return false;

        document = new MermaidDocument(kind, headerLine, lines);
        return true;
    }

    public static MermaidDocument Parse(string diagramText)
    {
        if (TryParse(diagramText, out var document) && document is not null)
            return document;

        throw new DiagramParseException("Diagram text cannot be null, empty, or an unsupported Mermaid diagram type.");
    }

    private static bool TryDetectKind(string headerLine, out MermaidDiagramKind kind)
    {
        var normalizedHeader = headerLine.Trim().ToLowerInvariant();
        if (normalizedHeader.StartsWith("graph ", StringComparison.Ordinal)
            || normalizedHeader.StartsWith("flowchart ", StringComparison.Ordinal)
            || normalizedHeader.Equals("graph", StringComparison.Ordinal)
            || normalizedHeader.Equals("flowchart", StringComparison.Ordinal))
        {
            kind = MermaidDiagramKind.Flowchart;
            return true;
        }

        kind = default;
        return false;
    }
}
