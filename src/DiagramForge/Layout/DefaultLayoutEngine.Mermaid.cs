using System.Collections.Frozen;
using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private delegate void MermaidLayoutHandler(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double hGap,
        double vGap,
        double pad);

    // Keep one registration per line so independent Mermaid diagram layout work
    // usually merges as adjacent insertions instead of colliding inside Layout().
    private static readonly FrozenDictionary<string, MermaidLayoutHandler> MermaidLayoutHandlers =
        new Dictionary<string, MermaidLayoutHandler>(StringComparer.OrdinalIgnoreCase)
        {
            ["architecture"] = (diagram, theme, minW, nodeH, hGap, vGap, pad) => LayoutArchitectureDiagram(diagram, theme, minW, nodeH, hGap, vGap, pad),
            ["block"] = (diagram, theme, minW, nodeH, hGap, vGap, pad) =>
            {
                bool hasEdges = diagram.Edges.Count > 0;
                double blockHGap = hasEdges ? BlockHGapWide : BlockHGapTight;
                double blockVGap = hasEdges ? BlockVGapWide : BlockVGapTight;
                LayoutBlockDiagram(diagram, theme, minW, nodeH, blockHGap, blockVGap, pad);
            },
            ["sequencediagram"] = (diagram, theme, minW, nodeH, hGap, vGap, pad) => LayoutSequenceDiagram(diagram, theme, minW, nodeH, hGap, vGap, pad),
            ["timeline"] = (diagram, theme, minW, nodeH, hGap, vGap, pad) => LayoutTimelineDiagram(diagram, theme, minW, nodeH, hGap, vGap, pad),
            ["venn"] = (diagram, theme, minW, nodeH, hGap, vGap, pad) => LayoutVennDiagram(diagram, theme, minW, pad),
            ["xychart"] = (diagram, theme, minW, nodeH, hGap, vGap, pad) => LayoutXyChartDiagram(diagram, theme, pad),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static bool TryLayoutMermaidDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double hGap,
        double vGap,
        double pad)
    {
        if (!MermaidLayoutHandlers.TryGetValue(diagram.DiagramType ?? string.Empty, out var handler))
            return false;

        handler(diagram, theme, minW, nodeH, hGap, vGap, pad);
        return true;
    }
}