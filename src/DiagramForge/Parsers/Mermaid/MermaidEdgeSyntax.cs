using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

/// <summary>
/// Shared edge operator utilities for Mermaid diagram parsers.
/// </summary>
internal static class MermaidEdgeSyntax
{
    /// <summary>
    /// Edge operator tokens in priority order (longest-match wins on a position tie).
    /// </summary>
    internal static readonly string[] Operators = ["--->", "-->>", "-.->", "==>", "===", "-->", "-.-", "---"];

    /// <summary>
    /// Finds the earliest (and longest, on a position tie) edge operator in <paramref name="line"/>.
    /// Returns <c>(null, -1)</c> when no operator is found.
    /// </summary>
    internal static (string? op, int index) FindOperator(string line)
    {
        string? matchedOp = null;
        int opIndex = -1;

        foreach (var op in Operators)
        {
            int idx = line.IndexOf(op, StringComparison.Ordinal);
            if (idx >= 0 && (opIndex < 0 || idx < opIndex || (idx == opIndex && op.Length > matchedOp!.Length)))
            {
                opIndex = idx;
                matchedOp = op;
            }
        }

        return (matchedOp, opIndex);
    }

    /// <summary>
    /// Returns the <see cref="EdgeLineStyle"/> implied by <paramref name="op"/>.
    /// </summary>
    internal static EdgeLineStyle LineStyleFor(string op) =>
        op.Contains('=') ? EdgeLineStyle.Thick
        : op.Contains('.') ? EdgeLineStyle.Dotted
        : EdgeLineStyle.Solid;

    /// <summary>
    /// Returns the <see cref="ArrowHeadStyle"/> implied by <paramref name="op"/>.
    /// </summary>
    internal static ArrowHeadStyle ArrowHeadFor(string op) =>
        op.Contains('>') ? ArrowHeadStyle.Arrow : ArrowHeadStyle.None;
}
