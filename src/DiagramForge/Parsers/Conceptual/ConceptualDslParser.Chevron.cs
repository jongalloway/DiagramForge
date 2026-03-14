using DiagramForge.Abstractions;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseChevronsDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
        => ParseListDiagram(lines, builder, "steps", "chevrons", minItems: 2);
}
