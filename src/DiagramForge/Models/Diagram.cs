namespace DiagramForge.Models;

/// <summary>
/// The root of the unified semantic diagram model.
/// Parsers produce a <see cref="Diagram"/>; the layout engine and SVG renderer consume it.
/// </summary>
public class Diagram
{
    /// <summary>Optional human-readable title of the diagram.</summary>
    public string? Title { get; set; }

    /// <summary>Identifies the source syntax that produced this model (e.g., "mermaid", "conceptual").</summary>
    public string? SourceSyntax { get; set; }

    /// <summary>Type of diagram (e.g., flowchart, venn, matrix, pyramid).</summary>
    public string? DiagramType { get; set; }

    /// <summary>All nodes in the diagram, keyed by their ID.</summary>
    public Dictionary<string, Node> Nodes { get; } = new();

    /// <summary>All directed edges between nodes.</summary>
    public List<Edge> Edges { get; } = new();

    /// <summary>Top-level groups / subgraphs (containers).</summary>
    public List<Group> Groups { get; } = new();

    /// <summary>Layout configuration for this diagram.</summary>
    public LayoutHints LayoutHints { get; set; } = new();

    /// <summary>Theme override for this diagram (null = use renderer default).</summary>
    public Theme? Theme { get; set; }

    /// <summary>Arbitrary parser-specific metadata shared with layout and rendering.</summary>
    public Dictionary<string, object> Metadata { get; } = new();

    // ── Convenience helpers ───────────────────────────────────────────────────

    /// <summary>Adds a node and returns the diagram (fluent).</summary>
    public Diagram AddNode(Node node)
    {
        Nodes[node.Id] = node;
        return this;
    }

    /// <summary>Adds an edge and returns the diagram (fluent).</summary>
    public Diagram AddEdge(Edge edge)
    {
        Edges.Add(edge);
        return this;
    }

    /// <summary>Adds a group and returns the diagram (fluent).</summary>
    public Diagram AddGroup(Group group)
    {
        Groups.Add(group);
        return this;
    }
}
