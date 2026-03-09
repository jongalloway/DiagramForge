using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

/// <summary>
/// Parses a subset of the Mermaid diagram syntax into a unified <see cref="Diagram"/> model.
/// </summary>
/// <remarks>
/// Supported Mermaid diagram types (v1 subset):
/// <list type="bullet">
///   <item>Flowchart (LR, RL, TB, BT, TD)</item>
/// </list>
/// </remarks>
public sealed class MermaidParser : IDiagramParser
{
    public string SyntaxId => "mermaid";

    /// <inheritdoc/>
    public bool CanParse(string diagramText)
    {
        if (string.IsNullOrWhiteSpace(diagramText))
            return false;

        var firstLine = diagramText.TrimStart().Split('\n')[0].Trim().ToLowerInvariant();
        return firstLine.StartsWith("graph ", StringComparison.Ordinal)
            || firstLine.StartsWith("flowchart ", StringComparison.Ordinal)
            || firstLine.Equals("graph", StringComparison.Ordinal)
            || firstLine.Equals("flowchart", StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public Diagram Parse(string diagramText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagramText);

        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax(SyntaxId)
            .WithDiagramType("flowchart");

        var lines = diagramText
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("%%", StringComparison.Ordinal))
            .ToArray();

        if (lines.Length == 0)
            throw new DiagramParseException("Diagram text is empty.");

        var headerLine = lines[0].ToLowerInvariant();
        var direction = ParseDirection(headerLine);
        builder.WithLayoutHints(new LayoutHints { Direction = direction });

        // Track nodes encountered so far (implicit node creation is allowed in Mermaid)
        var nodesSeen = new Dictionary<string, Node>(StringComparer.Ordinal);

        Node GetOrCreateNode(string id, string label)
        {
            if (!nodesSeen.TryGetValue(id, out var node))
            {
                node = new Node(id, label);
                nodesSeen[id] = node;
                builder.AddNode(node);
            }
            return node;
        }

        // Parse body lines (skip the header)
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];

            // Skip subgraph declarations (not yet fully supported)
            if (line.StartsWith("subgraph", StringComparison.OrdinalIgnoreCase)
                || line.Equals("end", StringComparison.OrdinalIgnoreCase))
                continue;

            ParseLine(line, builder, GetOrCreateNode);
        }

        return builder.Build();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LayoutDirection ParseDirection(string headerLine)
    {
        if (headerLine.Contains(" lr", StringComparison.Ordinal)) return LayoutDirection.LeftToRight;
        if (headerLine.Contains(" rl", StringComparison.Ordinal)) return LayoutDirection.RightToLeft;
        if (headerLine.Contains(" bt", StringComparison.Ordinal)) return LayoutDirection.BottomToTop;
        // TD and TB both map to TopToBottom
        return LayoutDirection.TopToBottom;
    }

    private static void ParseLine(
        string line,
        IDiagramSemanticModelBuilder builder,
        Func<string, string, Node> getOrCreate)
    {
        // Attempt to match an edge expression: A --> B, A -- text --> B, A --- B, etc.
        // We use a simple regex-free tokenizer for robustness.

        // Detect edge operators: -->, ---, -.->, -.-, ==>, ===
        string[] edgeOps = ["-->", "--->", "-->>", "-.->", "-.-", "==>", "===", "---"];

        string? matchedOp = null;
        int opIndex = -1;

        foreach (var op in edgeOps)
        {
            int idx = line.IndexOf(op, StringComparison.Ordinal);
            if (idx >= 0 && (opIndex < 0 || idx < opIndex))
            {
                opIndex = idx;
                matchedOp = op;
            }
        }

        if (matchedOp is not null)
        {
            // Split around the operator
            var left = line[..opIndex].Trim();
            var right = line[(opIndex + matchedOp.Length)..].Trim();

            // Edge label: A -- "label" --> B  => left part might contain label text
            string? edgeLabel = null;
            int pipeIdx = left.IndexOf('|');
            if (pipeIdx >= 0)
            {
                // Mermaid pipe-style label: A -->|label| B
                edgeLabel = left[(pipeIdx + 1)..].TrimEnd('|').Trim();
                left = left[..pipeIdx].Trim();
            }
            else if (right.StartsWith('|'))
            {
                int endPipe = right.IndexOf('|', 1);
                if (endPipe > 0)
                {
                    edgeLabel = right[1..endPipe].Trim();
                    right = right[(endPipe + 1)..].Trim();
                }
            }

            var (srcId, srcLabel) = ParseNodeDeclaration(left);
            var (tgtId, tgtLabel) = ParseNodeDeclaration(right);

            getOrCreate(srcId, srcLabel);
            getOrCreate(tgtId, tgtLabel);

            var edge = new Edge(srcId, tgtId);
            if (edgeLabel is not null)
                edge.Label = new Label(edgeLabel);

            edge.LineStyle = matchedOp.Contains('.') ? EdgeLineStyle.Dashed : EdgeLineStyle.Solid;
            edge.ArrowHead = matchedOp.Contains('>') ? ArrowHeadStyle.Arrow : ArrowHeadStyle.None;

            builder.AddEdge(edge);
        }
        else
        {
            // Standalone node declaration
            var (id, label) = ParseNodeDeclaration(line);
            if (!string.IsNullOrEmpty(id))
                getOrCreate(id, label);
        }
    }

    /// <summary>
    /// Parses a Mermaid node declaration such as:
    /// <c>A</c>, <c>A[Label]</c>, <c>A(Label)</c>, <c>A{Label}</c>, <c>A((Label))</c>
    /// </summary>
    private static (string id, string label) ParseNodeDeclaration(string token)
    {
        token = token.Trim();
        if (string.IsNullOrEmpty(token))
            return (string.Empty, string.Empty);

        // Identify the node id (alphanumeric prefix before any bracket)
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
            return (token, token);

        var id = token[..bracketStart].Trim();
        var rest = token[bracketStart..].Trim();

        // Strip surrounding brackets/parens/braces
        var label = rest
            .TrimStart('[', '(', '{', '>')
            .TrimEnd(']', ')', '}', '<')
            .Trim('"');

        // Determine shape from bracket style
        return (id, string.IsNullOrEmpty(label) ? id : label);
    }
}
