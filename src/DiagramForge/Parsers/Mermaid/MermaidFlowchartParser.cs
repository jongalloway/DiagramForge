using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidFlowchartParser : IMermaidDiagramParser
{
    private sealed class GroupFrame
    {
        public GroupFrame(Group group)
        {
            Group = group;
        }

        public Group Group { get; }

        public HashSet<string> Members { get; } = new(StringComparer.Ordinal);
    }

    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.Flowchart;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("flowchart");

        var direction = ParseDirection(document.HeaderLine.ToLowerInvariant());
        builder.WithLayoutHints(new LayoutHints { Direction = direction });

        var nodesSeen = new Dictionary<string, Node>(StringComparer.Ordinal);
        var groupStack = new Stack<GroupFrame>();
        int autoSubgraphId = 0;

        Node GetOrCreateNode(string id, string label)
        {
            if (!nodesSeen.TryGetValue(id, out var node))
            {
                node = new Node(id, label);
                nodesSeen[id] = node;
                builder.AddNode(node);
            }

            foreach (var frame in groupStack)
                frame.Members.Add(id);

            return node;
        }

        void CloseGroup()
        {
            var frame = groupStack.Pop();
            frame.Group.ChildNodeIds.AddRange(frame.Members.OrderBy(member => member, StringComparer.Ordinal));

            if (groupStack.Count > 0)
                groupStack.Peek().Group.ChildGroupIds.Add(frame.Group.Id);

            builder.AddGroup(frame.Group);
        }

        for (int i = 1; i < document.Lines.Length; i++)
        {
            var line = document.Lines[i];

            if (line.StartsWith("subgraph", StringComparison.OrdinalIgnoreCase)
                && (line.Length == 8 || char.IsWhiteSpace(line[8])))
            {
                var (id, title) = ParseSubgraphHeader(line.Length > 8 ? line[8..] : string.Empty);
                id ??= $"__subgraph{autoSubgraphId++}";
                groupStack.Push(new GroupFrame(new Group(id, title)));
                continue;
            }

            if (line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                if (groupStack.Count > 0)
                    CloseGroup();
                continue;
            }

            if (groupStack.Count > 0
                && line.StartsWith("direction ", StringComparison.OrdinalIgnoreCase))
            {
                groupStack.Peek().Group.Direction = ParseDirection(line.ToLowerInvariant());
                continue;
            }

            ParseLine(line, builder, GetOrCreateNode);
        }

        while (groupStack.Count > 0)
            CloseGroup();

        return builder.Build();
    }

    private static LayoutDirection ParseDirection(string headerLine)
    {
        if (headerLine.Contains(" lr", StringComparison.Ordinal)) return LayoutDirection.LeftToRight;
        if (headerLine.Contains(" rl", StringComparison.Ordinal)) return LayoutDirection.RightToLeft;
        if (headerLine.Contains(" bt", StringComparison.Ordinal)) return LayoutDirection.BottomToTop;
        return LayoutDirection.TopToBottom;
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

            string? edgeLabel = null;
            int pipeIdx = left.IndexOf('|');
            if (pipeIdx >= 0)
            {
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

            var (srcId, srcLabel, srcShape) = ParseNodeDeclaration(left);
            var (tgtId, tgtLabel, tgtShape) = ParseNodeDeclaration(right);

            var srcNode = getOrCreate(srcId, srcLabel);
            if (srcShape.HasValue) srcNode.Shape = srcShape.Value;

            var tgtNode = getOrCreate(tgtId, tgtLabel);
            if (tgtShape.HasValue) tgtNode.Shape = tgtShape.Value;

            var edge = new Edge(srcId, tgtId);
            if (edgeLabel is not null)
                edge.Label = new Label(edgeLabel);

            edge.LineStyle = MermaidEdgeSyntax.LineStyleFor(matchedOp);
            edge.ArrowHead = MermaidEdgeSyntax.ArrowHeadFor(matchedOp);

            builder.AddEdge(edge);
            return;
        }

        var (id, label, shape) = ParseNodeDeclaration(line);
        if (!string.IsNullOrEmpty(id))
        {
            var node = getOrCreate(id, label);
            if (shape.HasValue) node.Shape = shape.Value;
        }
    }

    private static (string id, string label, Shape? shape) ParseNodeDeclaration(string token) =>
        MermaidNodeSyntax.ParseNodeDeclaration(token);

    private static (string? id, string title) ParseSubgraphHeader(string remainder)
    {
        remainder = remainder.Trim();
        if (remainder.Length == 0)
            return (null, string.Empty);

        int sqStart = remainder.IndexOf('[');
        if (sqStart >= 0)
        {
            int sqEnd = remainder.LastIndexOf(']');
            if (sqEnd > sqStart)
            {
                string idPart = remainder[..sqStart].Trim();
                string title = remainder[(sqStart + 1)..sqEnd].Trim().Trim('"');
                return (idPart.Length > 0 ? idPart : null, title);
            }
        }

        if (remainder.Length >= 2 && remainder[0] == '"' && remainder[^1] == '"')
            return (null, remainder[1..^1]);

        if (!remainder.Any(char.IsWhiteSpace))
            return (remainder, remainder);

        return (null, remainder);
    }
}
