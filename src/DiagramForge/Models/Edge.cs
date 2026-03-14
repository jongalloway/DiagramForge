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

    /// <summary>Optional label displayed near the source end of the edge.</summary>
    public Label? SourceLabel { get; set; }

    /// <summary>Optional label displayed near the target end of the edge.</summary>
    public Label? TargetLabel { get; set; }

    /// <summary>Style of the edge line.</summary>
    public EdgeLineStyle LineStyle { get; set; } = EdgeLineStyle.Solid;

    /// <summary>Arrow head style at the target end.</summary>
    public ArrowHeadStyle ArrowHead { get; set; } = ArrowHeadStyle.Arrow;

    /// <summary>Arrow head style at the source end (e.g. diamond for UML composition/aggregation).</summary>
    public ArrowHeadStyle SourceArrowHead { get; set; } = ArrowHeadStyle.None;

    /// <summary>Whether the edge is bidirectional.</summary>
    public bool IsBidirectional { get; set; }

    /// <summary>Override stroke color (null = inherit from theme).</summary>
    public string? Color { get; set; }

    /// <summary>
    /// Per-edge routing override. When <see langword="null"/> the diagram-level
    /// <see cref="LayoutHints.EdgeRouting"/> default is used.
    /// </summary>
    public EdgeRouting? Routing { get; set; }

    /// <summary>Arbitrary metadata from the parser (e.g., port directions for architecture diagrams).</summary>
    public Dictionary<string, object> Metadata { get; } = new();
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

/// <summary>Controls the geometry used to draw edge paths.</summary>
public enum EdgeRouting
{
    /// <summary>Smooth cubic Bézier curve (default).</summary>
    Bezier,

    /// <summary>Axis-aligned (rectilinear) segments with subtly rounded corners.</summary>
    Orthogonal,

    /// <summary>Direct straight line from source anchor to target anchor.</summary>
    Straight,
}
