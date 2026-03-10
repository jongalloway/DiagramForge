using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidStateParser : IMermaidDiagramParser
{
    // Synthetic IDs for the [*] terminal markers.
    internal const string StartNodeId = "__start__";
    internal const string EndNodeId = "__end__";

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
        var (matchedOp, opIndex) = MermaidEdgeSyntax.FindOperator(line);

        if (matchedOp is not null)
        {
            var left = line[..opIndex].Trim();
            var right = line[(opIndex + matchedOp.Length)..].Trim();

            // State diagrams use `: label` suffix on the right side for transition labels.
            // Split on the first colon, trimming both sides to handle any whitespace variant
            // (e.g. "B : label", "B: label", "B:label").
            string? edgeLabel = null;
            int colonIdx = right.IndexOf(':');
            if (colonIdx >= 0)
            {
                var candidateLabel = right[(colonIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(candidateLabel))
                {
                    edgeLabel = candidateLabel;
                    right = right[..colonIdx].Trim();
                }
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

            edge.LineStyle = MermaidEdgeSyntax.LineStyleFor(matchedOp);
            edge.ArrowHead = MermaidEdgeSyntax.ArrowHeadFor(matchedOp);

            builder.AddEdge(edge);
            return;
        }

        // State definition: `id : Label` or `id:Label` — flexible colon handling.
        int defColon = line.IndexOf(':');
        if (defColon >= 0)
        {
            var id = line[..defColon].Trim();
            var label = line[(defColon + 1)..].Trim();
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(label))
            {
                var node = getOrCreate(id, label);
                // Always apply the explicit label from a definition line, overwriting
                // any label that was set when the node was first created via a transition.
                node.Label = new Label(label);
            }
        }
        else if (!string.IsNullOrWhiteSpace(line))
        {
            var id = line.Trim();
            getOrCreate(id, id);
        }
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
