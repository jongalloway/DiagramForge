using DiagramForge.Models;

namespace DiagramForge.Abstractions;

/// <summary>
/// Converts a laid-out <see cref="Diagram"/> to an SVG string.
/// </summary>
public interface ISvgRenderer
{
    /// <summary>
    /// Renders <paramref name="diagram"/> as an SVG document string.
    /// </summary>
    /// <param name="diagram">The diagram with layout already applied.</param>
    /// <param name="theme">The theme to use for styling.</param>
    /// <returns>A complete, self-contained SVG string.</returns>
    string Render(Diagram diagram, Theme theme);
}
