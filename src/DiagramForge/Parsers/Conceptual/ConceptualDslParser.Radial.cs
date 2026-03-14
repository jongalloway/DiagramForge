using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseRadialDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        int centerLine = FindSectionLine(lines, "center");
        if (centerLine < 0)
            throw new DiagramParseException("Missing required key 'center:' in radial diagram.");

        var centerTrimmed = lines[centerLine].Trim();
        var colonPos = centerTrimmed.IndexOf(':', StringComparison.Ordinal);
        var centerLabel = colonPos >= 0 ? centerTrimmed[(colonPos + 1)..].Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(centerLabel))
            throw new DiagramParseException("The 'center:' key must have a non-empty label in radial diagram.");

        int itemsLine = FindSectionLine(lines, "items");
        if (itemsLine < 0)
            throw new DiagramParseException("Missing required section 'items:' in radial diagram.");

        var items = ReadListItems(lines, itemsLine + 1);
        if (items.Count == 0)
            throw new DiagramParseException("Section 'items' contains no items.");

        if (items.Count < 3 || items.Count > 8)
            throw new DiagramParseException(
                $"Radial diagram requires between 3 and 8 items, but {items.Count} were provided.");

        var centerNode = new Node("center", centerLabel);
        centerNode.Metadata["radial:isCenter"] = true;
        builder.AddNode(centerNode);

        for (int i = 0; i < items.Count; i++)
        {
            var nodeId = $"item_{i}";
            var node = new Node(nodeId, items[i]);
            node.Metadata["radial:itemIndex"] = i;
            builder.AddNode(node);

            var edge = new Edge("center", nodeId) { Routing = EdgeRouting.Straight };
            builder.AddEdge(edge);
        }

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });
    }
}
