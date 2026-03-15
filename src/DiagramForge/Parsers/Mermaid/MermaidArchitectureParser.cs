using System.Text.RegularExpressions;
using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

using DiagramGroup = DiagramForge.Models.Group;

internal sealed partial class MermaidArchitectureParser : IMermaidDiagramParser
{
    // group id(icon)[Label]
    // group id(icon)[Label] in parentId
    [GeneratedRegex(@"^group\s+(?<id>\w+)\((?<icon>[^)]*)\)\[(?<label>[^\]]*)\](?:\s+in\s+(?<parent>\w+))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex GroupPattern();

    // service id(icon)[Label]
    // service id(icon)[Label] in groupId
    [GeneratedRegex(@"^service\s+(?<id>\w+)\((?<icon>[^)]*)\)\[(?<label>[^\]]*)\](?:\s+in\s+(?<parent>\w+))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ServicePattern();

    // junction id
    [GeneratedRegex(@"^junction\s+(?<id>\w+)$", RegexOptions.CultureInvariant)]
    private static partial Regex JunctionPattern();

    // Edge: src{group}:PORT --> PORT:dst{group}  (or -- for undirected)
    // The {group} suffix is optional and stripped from the node ID.
    // Arrow may be --> (directed) or -- (undirected).
    [GeneratedRegex(
        @"^(?<src>\w+)(?:\{group\})?:(?<srcPort>[LRTB])\s*(?<arrow>-->|--)\s*(?<dstPort>[LRTB]):(?<dst>\w+)(?:\{group\})?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex EdgePattern();

    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.ArchitectureDiagram;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("architecture");

        var diagram = builder.Build();

        // ── Pass 1: register all groups and collect parent relationships ──────────
        // Pre-registering every group ID allows forward references in `in parentId`
        // declarations to resolve correctly in pass 2.
        var groupParents = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < document.Lines.Length; i++)
        {
            var groupMatch = GroupPattern().Match(document.Lines[i]);
            if (!groupMatch.Success)
                continue;

            var id = groupMatch.Groups["id"].Value;
            var label = groupMatch.Groups["label"].Value;
            var parent = groupMatch.Groups["parent"].Value;

            diagram.AddGroup(new DiagramGroup(id, label));
            if (!string.IsNullOrEmpty(parent))
                groupParents[id] = parent;
        }

        var groups = diagram.Groups.ToDictionary(g => g.Id, StringComparer.Ordinal);

        // Apply group nesting now that all group IDs are registered.
        foreach (var (childId, parentId) in groupParents)
        {
            if (groups.TryGetValue(parentId, out var parentGroup))
                parentGroup.ChildGroupIds.Add(childId);
        }

        // ── Pass 2: services, junctions, and edges ────────────────────────────────
        for (int i = 1; i < document.Lines.Length; i++)
        {
            var line = document.Lines[i];

            // Skip group lines — already handled in pass 1.
            if (GroupPattern().IsMatch(line))
                continue;

            var serviceMatch = ServicePattern().Match(line);
            if (serviceMatch.Success)
            {
                var id = serviceMatch.Groups["id"].Value;
                var label = serviceMatch.Groups["label"].Value;
                var icon = serviceMatch.Groups["icon"].Value;
                var parent = serviceMatch.Groups["parent"].Value;

                var node = new Node(id, label) { Shape = MapIconToShape(icon), IconRef = icon };
                diagram.AddNode(node);

                if (!string.IsNullOrEmpty(parent) && groups.TryGetValue(parent, out var parentGroup))
                    parentGroup.ChildNodeIds.Add(id);

                continue;
            }

            var junctionMatch = JunctionPattern().Match(line);
            if (junctionMatch.Success)
            {
                var id = junctionMatch.Groups["id"].Value;
                var node = new Node(id, string.Empty) { Shape = Shape.Circle };
                diagram.AddNode(node);
                continue;
            }

            var edgeMatch = EdgePattern().Match(line);
            if (edgeMatch.Success)
            {
                var srcId = edgeMatch.Groups["src"].Value;
                var srcPort = edgeMatch.Groups["srcPort"].Value;
                var arrow = edgeMatch.Groups["arrow"].Value;
                var dstPort = edgeMatch.Groups["dstPort"].Value;
                var dstId = edgeMatch.Groups["dst"].Value;

                // Ensure nodes referenced in edges exist (they may appear in edge-only defs)
                EnsureNode(diagram, srcId);
                EnsureNode(diagram, dstId);

                var edge = new Edge(srcId, dstId)
                {
                    ArrowHead = arrow == "-->" ? ArrowHeadStyle.Arrow : ArrowHeadStyle.None,
                    LineStyle = EdgeLineStyle.Solid,
                };
                edge.Metadata["source:port"] = srcPort;
                edge.Metadata["target:port"] = dstPort;
                diagram.AddEdge(edge);
            }
        }

        return diagram;
    }

    // In Mermaid architecture diagrams every service node renders as a rounded
    // rectangle.  The icon token (cloud, database, server …) selects only the
    // artwork drawn *inside* the node, not the container shape.
    private static Shape MapIconToShape(string icon) => Shape.RoundedRectangle;

    private static void EnsureNode(Diagram diagram, string id)
    {
        if (!diagram.Nodes.ContainsKey(id))
            diagram.AddNode(new Node(id, id));
    }
}
