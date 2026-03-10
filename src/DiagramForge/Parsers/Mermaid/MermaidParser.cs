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
        if (string.IsNullOrWhiteSpace(diagramText))
            throw new DiagramParseException("Diagram text cannot be null or empty.");

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

        // Subgraph stack. Each frame tracks the Group under construction and the set of
        // node ids referenced between its `subgraph` and `end` markers. In Mermaid, every
        // node touched inside the block — both sides of an edge — becomes a member, so
        // membership is captured inside GetOrCreateNode rather than from statement shape.
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
            // Register with every open group on the stack so that nested subgraphs
            // correctly propagate membership to their ancestors, mirroring Mermaid's
            // flowDb.addSubGraph semantics (all enclosing groups include the node).
            // HashSet keeps membership idempotent if the node is referenced more
            // than once inside the same block.
            foreach (var frame in groupStack)
                frame.members.Add(id);
            return node;
        }

        void CloseGroup()
        {
            var (group, members) = groupStack.Pop();
            // Sort for determinism — HashSet enumeration order is undefined and
            // group membership flows into SVG output via layout.
            group.ChildNodeIds.AddRange(members.OrderBy(m => m, StringComparer.Ordinal));
            builder.AddGroup(group);
        }

        // Parse body lines (skip the header)
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];

            // subgraph [<id>] [[<title>]] — open a new group frame.
            // Guard the keyword with a word-boundary check so a hypothetical node id
            // like `subgraphNode` does not accidentally open a group.
            if (line.StartsWith("subgraph", StringComparison.OrdinalIgnoreCase)
                && (line.Length == 8 || char.IsWhiteSpace(line[8])))
            {
                var (id, title) = ParseSubgraphHeader(line.Length > 8 ? line[8..] : string.Empty);
                id ??= $"__subgraph{autoSubgraphId++}";
                // A bare `subgraph` with no title results in an empty label so the renderer's
                // "skip blank labels" check suppresses the synthetic id from showing.
                // Named subgraphs (e.g., `subgraph myId[My Title]`) always use the parsed title.
                var group = new Group(id, title);
                groupStack.Push((group, new HashSet<string>(StringComparer.Ordinal)));
                continue;
            }

            // end — close the innermost group. Stray `end` with no open group is
            // tolerated (mermaid's jison grammar treats `end` as a keyword, but a
            // lone one is a no-op here rather than a hard error, consistent with
            // this parser's generally lenient posture).
            if (line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                if (groupStack.Count > 0)
                    CloseGroup();
                continue;
            }

            // direction inside a subgraph is part of the Mermaid grammar but out of
            // scope for this pass (#14). Skip it so it is not misread as a standalone
            // node declaration named "direction TB".
            if (groupStack.Count > 0
                && line.StartsWith("direction ", StringComparison.OrdinalIgnoreCase))
                continue;

            ParseLine(line, builder, GetOrCreateNode);
        }

        // Lenient EOF: close any groups left open (missing `end`). Mermaid proper
        // would error; here we prefer a best-effort render.
        while (groupStack.Count > 0)
            CloseGroup();

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
        // Ordered longest-first so that when two operators match at the same
        // position the longer (more specific) one wins.
        string[] edgeOps = ["--->", "-->>", "-.->", "==>", "===", "-->", "-.-", "---"];

        string? matchedOp = null;
        int opIndex = -1;

        foreach (var op in edgeOps)
        {
            int idx = line.IndexOf(op, StringComparison.Ordinal);
            // Prefer the match that starts earliest; on a tie, prefer the longer operator.
            if (idx >= 0 && (opIndex < 0 || idx < opIndex || (idx == opIndex && op.Length > matchedOp!.Length)))
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
        }
        else
        {
            // Standalone node declaration
            var (id, label, shape) = ParseNodeDeclaration(line);
            if (!string.IsNullOrEmpty(id))
            {
                var node = getOrCreate(id, label);
                if (shape.HasValue) node.Shape = shape.Value;
            }
        }
    }

    /// <summary>
    /// Parses a Mermaid node declaration such as:
    /// <c>A</c>, <c>A[Label]</c>, <c>A(Label)</c>, <c>A{Label}</c>, <c>A((Label))</c>
    /// </summary>
    /// <returns>The node ID, display label, and shape inferred from bracket syntax (null if no brackets).</returns>
    private static (string id, string label, Shape? shape) ParseNodeDeclaration(string token)
    {
        token = token.Trim();
        if (string.IsNullOrEmpty(token))
            return (string.Empty, string.Empty, null);

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
            return (token, token, null);

        var id = token[..bracketStart].Trim();
        var rest = token[bracketStart..].Trim();

        // Infer shape from opening bracket — check "((" before "(" so the circle case wins.
        Shape? shape = rest.StartsWith("((", StringComparison.Ordinal) ? Shape.Circle
                     : rest[0] == '[' ? Shape.Rectangle
                     : rest[0] == '(' ? Shape.RoundedRectangle
                     : rest[0] == '{' ? Shape.Diamond
                     : (Shape?)null;

        // Strip surrounding brackets/parens/braces
        var label = rest
            .TrimStart('[', '(', '{', '>')
            .TrimEnd(']', ')', '}', '<')
            .Trim('"');

        return (id, string.IsNullOrEmpty(label) ? id : label, shape);
    }

    /// <summary>
    /// Parses the remainder of a <c>subgraph</c> header line into an id and display title.
    /// </summary>
    /// <remarks>
    /// Behaviour mirrors mermaid-js <c>flowDb.addSubGraph</c> + <c>subgraph.spec.js</c>:
    /// <list type="bullet">
    ///   <item><c>subgraph One</c>              → id <c>One</c>,   title <c>One</c></item>
    ///   <item><c>subgraph "Some Title"</c>     → id <i>auto</i>,  title <c>Some Title</c></item>
    ///   <item><c>subgraph Some Title</c>       → id <i>auto</i>,  title <c>Some Title</c> (unquoted multi-word → title-only)</item>
    ///   <item><c>subgraph ide1[One]</c>        → id <c>ide1</c>,  title <c>One</c></item>
    ///   <item><c>subgraph uid2["text"]</c>     → id <c>uid2</c>,  title <c>text</c> (quotes inside brackets stripped)</item>
    ///   <item><c>subgraph</c> (bare)           → id <i>auto</i>,  empty title (no label rendered)</item>
    /// </list>
    /// A <c>null</c> id means "caller should auto-generate one".
    /// </remarks>
    private static (string? id, string title) ParseSubgraphHeader(string remainder)
    {
        remainder = remainder.Trim();
        if (remainder.Length == 0)
            return (null, string.Empty);

        // id[Title]  /  id [Title]  /  [Title]
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

        // "Quoted Title" — title-only, id auto-assigned
        if (remainder.Length >= 2 && remainder[0] == '"' && remainder[^1] == '"')
            return (null, remainder[1..^1]);

        // Single token (no whitespace) → serves as both id and title
        if (!remainder.Any(char.IsWhiteSpace))
            return (remainder, remainder);

        // Unquoted multi-word → mermaid drops the id and treats the whole thing as the title
        return (null, remainder);
    }
}
