namespace DiagramForge.Models;

/// <summary>
/// An ordered section within a node, typically used for UML-style class compartments.
/// </summary>
public class NodeCompartment
{
    public NodeCompartment()
    {
    }

    public NodeCompartment(string? kind)
    {
        Kind = kind;
    }

    public NodeCompartment(string? kind, IEnumerable<Label> lines)
    {
        Kind = kind;
        Lines.AddRange(lines);
    }

    /// <summary>
    /// Optional compartment role metadata such as <c>title</c>, <c>attributes</c>, or <c>methods</c>.
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// Ordered label lines contained in the compartment.
    /// </summary>
    public List<Label> Lines { get; } = new();
}