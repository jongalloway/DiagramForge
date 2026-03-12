using DiagramForge.Abstractions;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParsePyramidDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
        => ParseListDiagram(lines, builder, "levels", "pyramid");
}