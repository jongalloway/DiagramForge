using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal interface IMermaidDiagramParser
{
    bool CanParse(MermaidDiagramKind kind);

    Diagram Parse(MermaidDocument document);
}
