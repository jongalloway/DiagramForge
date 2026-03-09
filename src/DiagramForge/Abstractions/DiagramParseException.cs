namespace DiagramForge.Abstractions;

/// <summary>
/// Thrown when a diagram parser cannot parse the supplied source text.
/// </summary>
public class DiagramParseException : Exception
{
    public DiagramParseException(string message)
        : base(message) { }

    public DiagramParseException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>Line number in the source text where the error was detected, if known.</summary>
    public int? LineNumber { get; init; }
}
