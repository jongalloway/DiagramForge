using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

/// <summary>
/// Dispatches Mermaid source text to the diagram-type-specific parser that can handle it.
/// </summary>
/// <remarks>
/// Supported Mermaid diagram types (v1 subset):
/// <list type="bullet">
///   <item>Flowchart (LR, RL, TB, BT, TD)</item>
///   <item>Mindmap</item>
///   <item>State diagram (stateDiagram / stateDiagram-v2)</item>
///   <item>Block diagram (block / block-beta)</item>
///   <item>Sequence diagram (sequenceDiagram)</item>
/// </list>
/// </remarks>
public sealed class MermaidParser : IDiagramParser
{
    private readonly IReadOnlyList<IMermaidDiagramParser> _diagramParsers =
    [
        new MermaidFlowchartParser(),
        new MermaidMindmapParser(),
        new MermaidStateParser(),
        new MermaidBlockParser(),
        new MermaidSequenceParser(),
    ];

    private static readonly string[] SupportedDiagramTypes = ["flowchart", "mindmap", "statediagram", "block", "sequencediagram"];

    public string SyntaxId => "mermaid";

    /// <inheritdoc/>
    public bool CanParse(string diagramText)
    {
        if (!MermaidDocument.TryParse(diagramText, out var document))
            return false;

        return _diagramParsers.Any(p => p.CanParse(document.Kind));
    }

    /// <inheritdoc/>
    public Diagram Parse(string diagramText)
    {
        var document = MermaidDocument.Parse(diagramText);
        var parser = _diagramParsers.FirstOrDefault(candidate => candidate.CanParse(document.Kind));
        if (parser is null)
        {
            var spaceIndex = document.HeaderLine.IndexOf(' ');
            var diagramTypeToken = spaceIndex >= 0 ? document.HeaderLine[..spaceIndex] : document.HeaderLine;
            throw new DiagramParseException(
                $"Unsupported Mermaid diagram type '{diagramTypeToken}'. " +
                $"Supported Mermaid types: {string.Join(", ", SupportedDiagramTypes)}");
        }

        return parser.Parse(document);
    }
}
