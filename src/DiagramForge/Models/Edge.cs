namespace DiagramForge.Models;

/// <summary>
/// A directed or undirected connection between two nodes.
/// </summary>
public class Edge
{
    public Edge(string sourceId, string targetId)
    {
        SourceId = sourceId;
        TargetId = targetId;
    }

    /// <summary>ID of the source node.</summary>
    public string SourceId { get; set; }

    /// <summary>ID of the target node.</summary>
    public string TargetId { get; set; }

    /// <summary>Optional label displayed on the edge.</summary>
    public Label? Label { get; set; }

    /// <summary>Style of the edge line.</summary>
    public EdgeLineStyle LineStyle { get; set; } = EdgeLineStyle.Solid;

    /// <summary>Arrow head style at the target end.</summary>
    public ArrowHeadStyle ArrowHead { get; set; } = ArrowHeadStyle.Arrow;

    /// <summary>Whether the edge is bidirectional.</summary>
    public bool IsBidirectional { get; set; }

    /// <summary>Override stroke color (null = inherit from theme).</summary>
    public string? Color { get; set; }
}

public enum EdgeLineStyle
{
    Solid,
    Dashed,
    Dotted,
    Thick,
}

public enum ArrowHeadStyle
{
    None,
    Arrow,
    OpenArrow,
    Diamond,
    Circle,
}
