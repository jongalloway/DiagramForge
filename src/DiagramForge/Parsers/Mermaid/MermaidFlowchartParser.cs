using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidFlowchartParser : IMermaidDiagramParser
{
    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.Flowchart;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("flowchart");

        var direction = ParseDirection(document.HeaderLine.ToLowerInvariant());
        builder.WithLayoutHints(new LayoutHints { Direction = direction });

        var nodesSeen = new Dictionary<string, Node>(StringComparer.Ordinal);
        var groupStack = new Stack<(Group group, HashSet<string> members)>();
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
                frame.members.Add(id);

            return node;
        }

        void CloseGroup()
        {
            var (group, members) = groupStack.Pop();
            group.ChildNodeIds.AddRange(members.OrderBy(member => member, StringComparer.Ordinal));
            builder.AddGroup(group);
        }

        for (int i = 1; i < document.Lines.Length; i++)
        {
            var line = document.Lines[i];

            if (line.StartsWith("subgraph", StringComparison.OrdinalIgnoreCase)
                && (line.Length == 8 || char.IsWhiteSpace(line[8])))
            {
                var (id, title) = ParseSubgraphHeader(line.Length > 8 ? line[8..] : string.Empty);
                id ??= $"__subgraph{autoSubgraphId++}";
                groupStack.Push((new Group(id, title), new HashSet<string>(StringComparer.Ordinal)));
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
                continue;

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
        string[] edgeOps = ["--->", "-->>", "-.->", "==>", "===", "-->", "-.-", "---"];

        string? matchedOp = null;
        int opIndex = -1;

        foreach (var op in edgeOps)
        {
            int idx = line.IndexOf(op, StringComparison.Ordinal);
            if (idx >= 0 && (opIndex < 0 || idx < opIndex || (idx == opIndex && op.Length > matchedOp!.Length)))
            {
                opIndex = idx;
                matchedOp = op;
            }
        }

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

            edge.LineStyle = matchedOp.Contains('=') ? EdgeLineStyle.Thick
                           : matchedOp.Contains('.') ? EdgeLineStyle.Dotted
                           : EdgeLineStyle.Solid;
            edge.ArrowHead = matchedOp.Contains('>') ? ArrowHeadStyle.Arrow : ArrowHeadStyle.None;

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

    private static (string id, string label, Shape? shape) ParseNodeDeclaration(string token)
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
