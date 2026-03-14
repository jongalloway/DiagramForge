using System.Collections.Frozen;
using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private delegate void ConceptualLayoutHandler(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad);

    // Keep one registration per line so independent conceptual diagram additions
    // usually merge cleanly as adjacent insertions.
    private static readonly FrozenDictionary<string, ConceptualLayoutHandler> ConceptualLayoutHandlers =
        new Dictionary<string, ConceptualLayoutHandler>(StringComparer.OrdinalIgnoreCase)
        {
            ["cycle"] = LayoutCycleDiagram,
            ["funnel"] = LayoutFunnelDiagram,
            ["matrix"] = LayoutMatrixDiagram,
            ["pillars"] = LayoutPillarsDiagram,
            ["pyramid"] = LayoutPyramidDiagram,
            ["radial"] = LayoutRadialDiagram,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static bool TryLayoutConceptualDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        if (!ConceptualLayoutHandlers.TryGetValue(diagram.DiagramType ?? string.Empty, out var handler))
            return false;

        handler(diagram, theme, minW, nodeH, pad);
        return true;
    }
}