namespace DiagramForge.Models;

/// <summary>
/// A container / subgraph that groups a set of child nodes.
/// </summary>
public class Group
{
    public Group(string id)
    {
        Id = id;
        Label = new Label(id);
    }

    public Group(string id, string labelText)
    {
        Id = id;
        Label = new Label(labelText);
    }

    /// <summary>Unique identifier within the diagram.</summary>
    public string Id { get; set; }

    /// <summary>Display label for the group.</summary>
    public Label Label { get; set; }

    /// <summary>IDs of the child nodes belonging to this group.</summary>
    public List<string> ChildNodeIds { get; } = new();

    /// <summary>IDs of nested child groups.</summary>
    public List<string> ChildGroupIds { get; } = new();

    /// <summary>Override fill color (null = inherit from theme).</summary>
    public string? FillColor { get; set; }

    /// <summary>Override stroke color (null = inherit from theme).</summary>
    public string? StrokeColor { get; set; }

    /// <summary>
    /// Optional local layout direction for this group's member nodes.
    /// When set, the layout engine arranges the group's members according to this
    /// direction instead of the diagram-wide direction.
    /// </summary>
    public LayoutDirection? Direction { get; set; }

    /// <summary>Layout position computed by the layout engine.</summary>
    public double X { get; set; }

    /// <summary>Layout position computed by the layout engine.</summary>
    public double Y { get; set; }

    /// <summary>Layout size computed by the layout engine.</summary>
    public double Width { get; set; }

    /// <summary>Layout size computed by the layout engine.</summary>
    public double Height { get; set; }

    /// <summary>Arbitrary key-value metadata attached to this group (e.g., parser-specific annotations).</summary>
    public Dictionary<string, object> Metadata { get; } = new(StringComparer.Ordinal);
}
