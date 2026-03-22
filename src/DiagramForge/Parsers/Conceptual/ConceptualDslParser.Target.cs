using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseTargetDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        int titleLine = FindSectionLine(lines, "title");
        if (titleLine >= 0)
        {
            string titleValue = lines[titleLine].Trim()["title:".Length..].Trim();
            if (!string.IsNullOrEmpty(titleValue))
                builder.WithTitle(titleValue);
        }

        int centerLine = FindSectionLine(lines, "center");
        if (centerLine < 0)
            throw new DiagramParseException("Missing required key 'center:' in target diagram.");

        string centerRaw = lines[centerLine].Trim();
        int centerColon = centerRaw.IndexOf(':', StringComparison.Ordinal);
        string centerText = centerColon >= 0 ? centerRaw[(centerColon + 1)..].Trim() : string.Empty;
        var centerSpec = ParseIconLabeledText(centerText);

        if (string.IsNullOrWhiteSpace(centerSpec.Label))
            throw new DiagramParseException("The 'center:' key must have a non-empty label in target diagram.");

        int ringsLine = FindSectionLine(lines, "rings");
        if (ringsLine < 0)
            throw new DiagramParseException("Missing required section 'rings:' in target diagram.");

        var rings = ReadListItems(lines, ringsLine + 1);
        if (rings.Count == 0)
            throw new DiagramParseException("Section 'rings' contains no items.");

        if (rings.Count < 2 || rings.Count > 5)
            throw new DiagramParseException(
                $"Target diagram requires between 2 and 5 rings, but {rings.Count} were provided.");

        var parsedRings = new List<(string Label, string? Description)>();

        for (int i = 0; i < rings.Count; i++)
        {
            string raw = rings[i];
            string ringLabel;
            string? description = null;

            int colonIdx = raw.IndexOf(':');
            if (colonIdx >= 0)
            {
                ringLabel = raw[..colonIdx].Trim();
                string desc = raw[(colonIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(desc))
                    description = desc;
            }
            else
            {
                ringLabel = raw.Trim();
            }

            if (string.IsNullOrWhiteSpace(ringLabel))
                throw new DiagramParseException($"Target ring {i + 1} must have a non-empty label.");

            parsedRings.Add((ringLabel, description));

            var ringNode = new Node($"ring_{i}", ringLabel);
            ringNode.Metadata["target:kind"] = "ring";
            ringNode.Metadata["target:ringIndex"] = i;
            if (description is not null)
                ringNode.Metadata["target:description"] = description;
            builder.AddNode(ringNode);
        }

        var centerNode = new Node("center", centerSpec.Label) { IconRef = centerSpec.IconRef };
        centerNode.Metadata["target:kind"] = "center";
        builder.AddNode(centerNode);

        for (int i = 0; i < parsedRings.Count; i++)
        {
            var (label, description) = parsedRings[i];
            var cardNode = new Node($"card_{i}", label);
            cardNode.Metadata["target:kind"] = "card";
            cardNode.Metadata["target:ringIndex"] = i;

            if (description is not null)
                cardNode.Metadata["target:description"] = description;

            builder.AddNode(cardNode);
            builder.AddEdge(new Edge($"ring_{i}", $"card_{i}")
            {
                ArrowHead = ArrowHeadStyle.None,
                Routing = EdgeRouting.Straight,
            });
        }

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });
    }
}