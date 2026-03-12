using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseCycleDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        int sectionLine = FindSectionLine(lines, "steps");
        if (sectionLine < 0)
            throw new DiagramParseException("Missing required section 'steps:' in cycle diagram.");

        var items = ReadListItems(lines, sectionLine + 1);
        if (items.Count == 0)
            throw new DiagramParseException("Section 'steps' contains no items.");

        if (items.Count < 3 || items.Count > 6)
            throw new DiagramParseException(
                $"Cycle diagram requires between 3 and 6 steps, but {items.Count} were provided.");

        for (int i = 0; i < items.Count; i++)
        {
            var nodeId = $"node_{i}";
            var node = new Node(nodeId, items[i]);
            node.Metadata["cycle:stepIndex"] = i;
            builder.AddNode(node);
        }

        for (int i = 0; i < items.Count; i++)
        {
            var sourceId = $"node_{i}";
            var targetId = $"node_{(i + 1) % items.Count}";
            builder.AddEdge(new Edge(sourceId, targetId));
        }

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });
    }
}