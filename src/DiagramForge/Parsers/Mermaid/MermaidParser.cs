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
/// </list>
/// </remarks>
public sealed class MermaidParser : IDiagramParser
{
    private readonly IReadOnlyList<IMermaidDiagramParser> _diagramParsers =
    [
        new MermaidFlowchartParser(),
    ];

    private static readonly string[] SupportedDiagramTypes = ["flowchart"];

    public string SyntaxId => "mermaid";

    /// <inheritdoc/>
    public bool CanParse(string diagramText)
    {
        return MermaidDocument.TryParse(diagramText, out _);
    }

    /// <inheritdoc/>
    public Diagram Parse(string diagramText)
    {
        var document = MermaidDocument.Parse(diagramText);
        var parser = _diagramParsers.FirstOrDefault(candidate => candidate.CanParse(document.Kind));
        if (parser is null)
        {
            throw new DiagramParseException(
                $"Mermaid diagram type '{document.Kind}' is not supported. " +
                $"Supported Mermaid types: {string.Join(", ", SupportedDiagramTypes)}");
        }

        return parser.Parse(document);
    }
}
