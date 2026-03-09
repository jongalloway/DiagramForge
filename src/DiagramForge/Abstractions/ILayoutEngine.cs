using DiagramForge.Models;

namespace DiagramForge.Abstractions;

/// <summary>
/// Computes the 2-D layout (X, Y, Width, Height) for every element in a <see cref="Diagram"/>.
/// The layout engine must be called before the SVG renderer.
/// </summary>
public interface ILayoutEngine
{
    /// <summary>
    /// Applies layout information to all nodes and groups in <paramref name="diagram"/>,
    /// mutating their <c>X</c>, <c>Y</c>, <c>Width</c>, and <c>Height</c> properties in place.
    /// </summary>
    void Layout(Diagram diagram, Theme theme);
}
