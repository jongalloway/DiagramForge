using DiagramForge.Abstractions;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseFunnelDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
        => ParseListDiagram(lines, builder, "stages", "funnel");
}
