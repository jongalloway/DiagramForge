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

    /// <summary>Minimum height of a node (in SVG user units).</summary>
    public double MinNodeHeight { get; set; } = 40;

    /// <summary>Padding inside nodes (in SVG user units).</summary>
    public double NodePadding { get; set; } = 12;
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
