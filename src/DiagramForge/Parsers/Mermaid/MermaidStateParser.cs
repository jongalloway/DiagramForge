using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidStateParser : IMermaidDiagramParser
{
    // Synthetic IDs for the [*] terminal markers.
    internal const string StartNodeId = "__start__";
    internal const string EndNodeId = "__end__";

    private static readonly string[] EdgeOperators = ["--->", "-->>", "-.->", "==>", "===", "-->", "-.-", "---"];

    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.StateDiagram;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("statediagram");

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.TopToBottom });

        var nodesSeen = new Dictionary<string, Node>(StringComparer.Ordinal);

        Node GetOrCreateNode(string id, string label)
        {
            if (!nodesSeen.TryGetValue(id, out var node))
            {
                var shape = (id == StartNodeId || id == EndNodeId) ? Shape.Circle : Shape.Rectangle;
                node = new Node(id, label) { Shape = shape };
                nodesSeen[id] = node;
                builder.AddNode(node);
            }

            return node;
        }

        for (int i = 1; i < document.Lines.Length; i++)
        {
            var line = document.Lines[i];
            ParseLine(line, builder, GetOrCreateNode);
        }

        return builder.Build();
    }

    private static void ParseLine(
        string line,
        IDiagramSemanticModelBuilder builder,
        Func<string, string, Node> getOrCreate)
    {
        var (matchedOp, opIndex) = FindEdgeOperator(line);

        if (matchedOp is not null)
        {
            var left = line[..opIndex].Trim();
            var right = line[(opIndex + matchedOp.Length)..].Trim();

            // State diagrams use `: label` suffix on the right side for transition labels.
            string? edgeLabel = null;
            int colonIdx = right.IndexOf(" : ", StringComparison.Ordinal);
            if (colonIdx >= 0)
            {
                edgeLabel = right[(colonIdx + 3)..].Trim();
                right = right[..colonIdx].Trim();
            }

            var srcId = ResolveTerminalId(left, isSource: true);
            var tgtId = ResolveTerminalId(right, isSource: false);

            var srcLabel = srcId == StartNodeId ? "[*]" : srcId;
            var tgtLabel = tgtId == EndNodeId ? "[*]" : tgtId;

            getOrCreate(srcId, srcLabel);
            getOrCreate(tgtId, tgtLabel);

            var edge = new Edge(srcId, tgtId);
            if (edgeLabel is not null)
                edge.Label = new Label(edgeLabel);

            edge.LineStyle = matchedOp.Contains('=') ? EdgeLineStyle.Thick
                           : matchedOp.Contains('.') ? EdgeLineStyle.Dotted
                           : EdgeLineStyle.Solid;
            edge.ArrowHead = matchedOp.Contains('>') ? ArrowHeadStyle.Arrow : ArrowHeadStyle.None;

            builder.AddEdge(edge);
            return;
        }

        // State definition: `id` or `id : Label`
        int defColon = line.IndexOf(" : ", StringComparison.Ordinal);
        if (defColon >= 0)
        {
            var id = line[..defColon].Trim();
            var label = line[(defColon + 3)..].Trim();
            if (!string.IsNullOrEmpty(id))
                getOrCreate(id, label);
        }
        else if (!string.IsNullOrWhiteSpace(line))
        {
            var id = line.Trim();
            getOrCreate(id, id);
        }
    }

    /// <summary>
    /// Finds the earliest (and longest, on a tie) edge operator in <paramref name="line"/>.
    /// </summary>
    private static (string? op, int index) FindEdgeOperator(string line)
    {
        string? matchedOp = null;
        int opIndex = -1;

        foreach (var op in EdgeOperators)
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
    /// Maps the literal <c>[*]</c> token to its synthetic node ID based on whether
    /// it appears as a transition source (<c>__start__</c>) or target (<c>__end__</c>).
    /// </summary>
    private static string ResolveTerminalId(string token, bool isSource) =>
        token == "[*]"
            ? (isSource ? StartNodeId : EndNodeId)
            : token;
}
