using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseChevronsDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        int sectionLine = FindSectionLine(lines, "steps");
        if (sectionLine < 0)
            throw new DiagramParseException("Missing required section 'steps:' in chevrons diagram.");

        var items = ReadListItems(lines, sectionLine + 1);
        if (items.Count == 0)
            throw new DiagramParseException("Section 'steps' contains no items.");

        if (items.Count < 2)
            throw new DiagramParseException(
                $"Chevrons diagram requires at least 2 steps, but {items.Count} was provided.");

        for (int i = 0; i < items.Count; i++)
        {
            var nodeId = $"node_{i}";
            builder.AddNode(new Node(nodeId, items[i]));
        }

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });
    }
}
