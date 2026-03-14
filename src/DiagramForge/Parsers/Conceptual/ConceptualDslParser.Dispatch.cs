using System.Collections.Frozen;
using DiagramForge.Abstractions;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private delegate void ConceptualDiagramParseHandler(string[] lines, IDiagramSemanticModelBuilder builder);

    // Keep one handler registration per line so parallel diagram-type additions
    // usually merge as adjacent insertions instead of colliding inside a switch.
    private static readonly FrozenDictionary<string, ConceptualDiagramParseHandler> ParseHandlers =
        new Dictionary<string, ConceptualDiagramParseHandler>(StringComparer.OrdinalIgnoreCase)
        {
            ["cycle"] = ParseCycleDiagram,
            ["funnel"] = ParseFunnelDiagram,
            ["matrix"] = ParseMatrixDiagram,
            ["pillars"] = ParsePillarsDiagram,
            ["pyramid"] = ParsePyramidDiagram,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> KnownTypes =
        ParseHandlers.Keys.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}