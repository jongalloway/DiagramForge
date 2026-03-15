using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParsePillarsDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        int pillarsLine = FindSectionLine(lines, "pillars");
        if (pillarsLine < 0)
            throw new DiagramParseException("Missing required section 'pillars:' in pillars diagram.");

        var pillars = ReadPillars(lines, pillarsLine + 1);

        if (pillars.Count == 0)
            throw new DiagramParseException("Section 'pillars' contains no pillar entries.");

        if (pillars.Count < 2 || pillars.Count > 5)
            throw new DiagramParseException("Pillars diagram requires between 2 and 5 pillars.");

        for (int i = 0; i < pillars.Count; i++)
        {
            var (title, segments) = pillars[i];

            var titleNode = new Node($"pillar_{i}", title.Label) { IconRef = title.IconRef };
            titleNode.Metadata["pillars:pillarIndex"] = i;
            titleNode.Metadata["pillars:kind"] = "title";
            builder.AddNode(titleNode);

            for (int j = 0; j < segments.Count; j++)
            {
                var segNode = new Node($"pillar_{i}_segment_{j}", segments[j].Label) { IconRef = segments[j].IconRef };
                segNode.Metadata["pillars:pillarIndex"] = i;
                segNode.Metadata["pillars:segmentIndex"] = j;
                segNode.Metadata["pillars:kind"] = "segment";
                builder.AddNode(segNode);
            }
        }

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });
    }
}