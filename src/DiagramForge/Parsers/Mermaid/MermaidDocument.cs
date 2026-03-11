using DiagramForge.Abstractions;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidDocument
{
    private MermaidDocument(MermaidDiagramKind kind, string headerLine, string[] lines, string[] rawLines)
    {
        Kind = kind;
        HeaderLine = headerLine;
        Lines = lines;
        RawLines = rawLines;
    }

    public MermaidDiagramKind Kind { get; }

    public string HeaderLine { get; }

    /// <summary>Content lines with leading whitespace preserved (comments and blank lines filtered).</summary>
    public string[] RawLines { get; }

    public string[] Lines { get; }

    public static bool TryParse(string diagramText, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MermaidDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(diagramText))
            return false;

        var lines = diagramText
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("%%", StringComparison.Ordinal))
            .ToArray();

        var rawLines = diagramText
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("%%", StringComparison.Ordinal))
            .ToArray();

        if (lines.Length == 0)
            return false;

        var headerLine = lines[0];
        if (!TryDetectKind(headerLine, out var kind))
            return false;

        document = new MermaidDocument(kind, headerLine, lines, rawLines);
        return true;
    }

    // Known Mermaid diagram-type keywords (lowercased) that are recognized but not yet
    // supported by any registered IMermaidDiagramParser. Detecting them as Unknown lets
    // MermaidParser emit a specific "unsupported type" error instead of a generic one.
    private static readonly HashSet<string> KnownUnsupportedMermaidKeywords = new(StringComparer.Ordinal)
    {
        "classdiagram",
        "erdiagram",
        "journey",
        "gantt",
        "pie",
        "gitgraph",
        "quadrantchart",
        "requirementdiagram",
        "packet-beta",
        "kanban",
        "sankey-beta",
        "zenuml",
        "radar-beta",
        "treemap-beta",
    };

    public static MermaidDocument Parse(string diagramText)
    {
        if (TryParse(diagramText, out var document))
            return document;

        if (string.IsNullOrWhiteSpace(diagramText))
            throw new DiagramParseException("Diagram text cannot be null or whitespace.");

        var firstContentLine = diagramText
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrEmpty(line) && !line.StartsWith("%%", StringComparison.Ordinal));

        if (firstContentLine is null)
            throw new DiagramParseException("Diagram text cannot be empty or contain only comments.");

        throw new DiagramParseException($"Unsupported Mermaid diagram type '{firstContentLine}'.");
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

        if (normalizedHeader.Equals("mindmap", StringComparison.Ordinal))
        {
            kind = MermaidDiagramKind.Mindmap;
            return true;
        }

        if (normalizedHeader.Equals("venn-beta", StringComparison.Ordinal))
        {
            kind = MermaidDiagramKind.VennDiagram;
            return true;
        }

        if (normalizedHeader.Equals("statediagram", StringComparison.Ordinal)
            || normalizedHeader.Equals("statediagram-v2", StringComparison.Ordinal))
        {
            kind = MermaidDiagramKind.StateDiagram;
            return true;
        }

        if (normalizedHeader.Equals("block", StringComparison.Ordinal)
            || normalizedHeader.Equals("block-beta", StringComparison.Ordinal))
        {
            kind = MermaidDiagramKind.BlockDiagram;
            return true;
        }

        if (normalizedHeader.Equals("sequencediagram", StringComparison.Ordinal))
        {
            kind = MermaidDiagramKind.SequenceDiagram;
            return true;
        }

        if (normalizedHeader.Equals("timeline", StringComparison.Ordinal))
        {
            kind = MermaidDiagramKind.Timeline;
            return true;
        }

        if (normalizedHeader.Equals("architecture-beta", StringComparison.Ordinal))
        {
            kind = MermaidDiagramKind.ArchitectureDiagram;
            return true;
        }
        if (normalizedHeader.Equals("xychart-beta", StringComparison.OrdinalIgnoreCase))
        {
            kind = MermaidDiagramKind.XyChart;
            return true;
        }
        var spaceIndex = normalizedHeader.IndexOf(' ');
        var keyword = spaceIndex >= 0 ? normalizedHeader[..spaceIndex] : normalizedHeader;
        if (KnownUnsupportedMermaidKeywords.Contains(keyword))
        {
            kind = MermaidDiagramKind.Unknown;
            return true;
        }

        kind = default;
        return false;
    }
}
