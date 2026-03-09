using DiagramForge.Models;

namespace DiagramForge.Abstractions;

/// <summary>
/// Parses raw diagram source text and produces a unified <see cref="Diagram"/> semantic model.
/// </summary>
/// <remarks>
/// Implement this interface to add support for a new diagram syntax (e.g., D2, DOT).
/// The parser must not depend on any specific renderer or layout engine.
/// </remarks>
public interface IDiagramParser
{
    /// <summary>
    /// A short, lowercase identifier for the syntax this parser handles (e.g., "mermaid", "conceptual").
    /// </summary>
    string SyntaxId { get; }

    /// <summary>
    /// Returns <see langword="true"/> if this parser can handle the given <paramref name="diagramText"/>.
    /// Implementations may inspect the first line or use heuristics.
    /// </summary>
    bool CanParse(string diagramText);

    /// <summary>
    /// Parses <paramref name="diagramText"/> and returns a populated <see cref="Diagram"/>.
    /// </summary>
    /// <exception cref="DiagramParseException">Thrown when the text cannot be parsed.</exception>
    Diagram Parse(string diagramText);
}
