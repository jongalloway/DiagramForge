namespace DiagramForge.Models;

/// <summary>
/// A single node (box, shape) in the diagram's semantic model.
/// </summary>
public class Node
{
    public Node(string id)
    {
        Id = id;
        Label = new Label(id);
    }

    public Node(string id, string labelText)
    {
        Id = id;
        Label = new Label(labelText);
    }

    /// <summary>Unique identifier within the diagram.</summary>
    public string Id { get; set; }

    /// <summary>Display label for the node.</summary>
    public Label Label { get; set; }

    /// <summary>Visual shape of the node.</summary>
    public Shape Shape { get; set; } = Shape.RoundedRectangle;

    /// <summary>Override fill color (null = inherit from theme).</summary>
    public string? FillColor { get; set; }

    /// <summary>Override stroke color (null = inherit from theme).</summary>
    public string? StrokeColor { get; set; }

    /// <summary>Arbitrary metadata from the parser (e.g., Mermaid node type).</summary>
    public Dictionary<string, object> Metadata { get; } = new();

    /// <summary>
    /// Optional stereotype or annotation labels rendered above the node's primary title.
    /// </summary>
    public List<Label> Annotations { get; } = new();

    /// <summary>
    /// Optional ordered compartments rendered within the node body.
    /// </summary>
    public List<NodeCompartment> Compartments { get; } = new();

    /// <summary>Layout position computed by the layout engine.</summary>
    public double X { get; set; }

    /// <summary>Layout position computed by the layout engine.</summary>
    public double Y { get; set; }

    /// <summary>Layout size computed by the layout engine.</summary>
    public double Width { get; set; }

    /// <summary>Layout size computed by the layout engine.</summary>
    public double Height { get; set; }
}
