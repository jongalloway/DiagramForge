using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

/// <summary>
/// Shared node declaration parsing utilities for Mermaid diagram parsers.
/// </summary>
internal static class MermaidNodeSyntax
{
    /// <summary>
    /// Parses a single Mermaid node token (e.g. <c>A[Label]</c>, <c>root((Circle))</c>)
    /// and returns the node id, display label, and optional shape.
    /// </summary>
    internal static (string id, string label, Shape? shape) ParseNodeDeclaration(string token)
    {
        token = token.Trim();
        if (string.IsNullOrEmpty(token))
            return (string.Empty, string.Empty, null);

        int bracketStart = -1;
        for (int i = 0; i < token.Length; i++)
        {
            char c = token[i];
            if (c == '[' || c == '(' || c == '{' || c == '>')
            {
                bracketStart = i;
                break;
            }
        }

        if (bracketStart < 0)
            return (token, token, null);

        var id = token[..bracketStart].Trim();
        var rest = token[bracketStart..].Trim();

        Shape? shape = rest.StartsWith("((", StringComparison.Ordinal) ? Shape.Circle
                     : rest[0] == '[' ? Shape.Rectangle
                     : rest[0] == '(' ? Shape.RoundedRectangle
                     : rest[0] == '{' ? Shape.Diamond
                     : (Shape?)null;

        var label = rest
            .TrimStart('[', '(', '{', '>')
            .TrimEnd(']', ')', '}', '<')
            .Trim('"');

        return (id, string.IsNullOrEmpty(label) ? id : label, shape);
    }
}
