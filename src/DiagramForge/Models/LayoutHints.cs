namespace DiagramForge.Models;

/// <summary>
/// Hints that guide the layout engine when arranging diagram elements.
/// </summary>
public class LayoutHints
{
    /// <summary>Primary flow direction of the diagram.</summary>
    public LayoutDirection Direction { get; set; } = LayoutDirection.TopToBottom;

    /// <summary>Horizontal spacing between nodes (in SVG user units).</summary>
    public double HorizontalSpacing { get; set; } = 60;

    /// <summary>Vertical spacing between nodes (in SVG user units).</summary>
    public double VerticalSpacing { get; set; } = 40;

    /// <summary>Alignment of nodes within their container.</summary>
    public NodeAlignment Alignment { get; set; } = NodeAlignment.Center;

    /// <summary>Minimum width of a node (in SVG user units).</summary>
    public double MinNodeWidth { get; set; } = 120;

    /// <summary>Maximum width of a node before text wrapping kicks in.</summary>
    public double? MaxNodeWidth { get; set; } = 240;

    /// <summary>Minimum height of a node (in SVG user units).</summary>
    public double MinNodeHeight { get; set; } = 40;

    /// <summary>Padding inside nodes (in SVG user units).</summary>
    public double NodePadding { get; set; } = 12;

    /// <summary>
    /// Default edge routing style for all edges in the diagram.
    /// Individual edges may override this via <see cref="Edge.Routing"/>.
    /// </summary>
    public EdgeRouting EdgeRouting { get; set; } = EdgeRouting.Bezier;

    /// <summary>
    /// Corner radius used when <see cref="EdgeRouting"/> is
    /// <see cref="EdgeRouting.Orthogonal"/> (in SVG user units).
    /// </summary>
    public double OrthogonalCornerRadius { get; set; } = 6.0;
}

/// <summary>The primary direction of diagram flow.</summary>
public enum LayoutDirection
{
    TopToBottom,
    BottomToTop,
    LeftToRight,
    RightToLeft,
}

/// <summary>Horizontal alignment of nodes within their container.</summary>
public enum NodeAlignment
{
    Left,
    Center,
    Right,
}
