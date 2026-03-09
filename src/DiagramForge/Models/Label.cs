namespace DiagramForge.Models;

/// <summary>
/// A text label that can be attached to a node or edge.
/// </summary>
public class Label
{
    public Label(string text)
    {
        Text = text;
    }

    /// <summary>The display text of the label.</summary>
    public string Text { get; set; }

    /// <summary>Optional tooltip / long-form description.</summary>
    public string? Tooltip { get; set; }

    /// <summary>Override font size (null = inherit from theme).</summary>
    public double? FontSize { get; set; }

    /// <summary>Override text color (null = inherit from theme).</summary>
    public string? Color { get; set; }
}
