using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidClassDiagramParser : IMermaidDiagramParser
{
    // Relationship operators sorted so that longer tokens are tried first when positions tie.
    // Each entry: (token, relationshipType, isReversed).
    // isReversed=true means the written textual order is source-on-right / target-on-left,
    // so SourceId and TargetId must be swapped relative to the left/right parse order.
    // For example: "Animal <|-- Dog" → left=Animal, right=Dog, but Dog IS the child
    // (semantic source), so isReversed=true causes Edge(Dog, Animal).
    private static readonly (string Token, string RelType, bool IsReversed)[] RelOperators =
    [
        ("<|--", "inheritance", true),
        ("--|>", "inheritance", false),
        ("<|..", "realization", true),
        ("..|>", "realization", false),
        ("*--",  "composition", false),
        ("--*",  "composition", true),
        ("o--",  "aggregation", false),
        ("--o",  "aggregation", true),
        ("<..",  "dependency",  true),
        ("..>",  "dependency",  false),
        ("<--",  "association", true),
        ("-->",  "association", false),
        ("..",   "link",        false),
        ("--",   "link",        false),
    ];

    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.ClassDiagram;

    public Diagram Parse(MermaidDocument document)
    {
        var hints = new LayoutHints { Direction = LayoutDirection.TopToBottom };

        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("classdiagram")
            .WithLayoutHints(hints);

        var nodesSeen = new Dictionary<string, Node>(StringComparer.Ordinal);

        Node GetOrCreateNode(string id)
        {
            if (!nodesSeen.TryGetValue(id, out var node))
            {
                node = new Node(id, id) { Shape = Shape.Rectangle };
                node.Metadata["class:isClass"] = true;
                nodesSeen[id] = node;
                builder.AddNode(node);
            }

            return node;
        }

        // Current class whose {} block is being parsed (null = top-level).
        Node? currentBlockNode = null;

        for (int i = 1; i < document.Lines.Length; i++)
        {
            var line = document.Lines[i];

            // ── Close brace ends a member block ─────────────────────────────
            if (line == "}")
            {
                currentBlockNode = null;
                continue;
            }

            // ── Lines inside a member block ──────────────────────────────────
            if (currentBlockNode is not null)
            {
                AddMemberToNode(currentBlockNode, line);
                continue;
            }

            // ── direction ────────────────────────────────────────────────────
            if (line.StartsWith("direction ", StringComparison.OrdinalIgnoreCase))
            {
                hints.Direction = ParseDirection(line);
                continue;
            }

            // ── Explicit class declaration: "class ClassName" or
            //    "class ClassName[\"label\"]" or "class ClassName {"  ─────────
            if (line.StartsWith("class ", StringComparison.OrdinalIgnoreCase))
            {
                var rest = line[6..].Trim();
                bool opensBrace = rest.EndsWith('{');
                if (opensBrace)
                    rest = rest[..^1].TrimEnd();

                var (id, label) = ParseClassDeclaration(rest);
                if (!string.IsNullOrEmpty(id))
                {
                    var node = GetOrCreateNode(id);
                    if (label is not null)
                        node.Label = new Label(label);

                    if (opensBrace)
                        currentBlockNode = node;
                }

                continue;
            }

            // ── ClassName { member block (no "class" keyword) ────────────────
            if (line.EndsWith('{'))
            {
                var candidate = line[..^1].TrimEnd();
                if (IsValidClassId(candidate) && FindRelationshipOp(candidate) is null)
                {
                    currentBlockNode = GetOrCreateNode(candidate);
                    continue;
                }
            }

            // ── Relationship line (try before colon-member to avoid false ────
            //    positives on "ClassName : member" that looks like a target)   ─
            if (TryParseRelationship(line, builder, GetOrCreateNode))
                continue;

            // ── Colon-member: "ClassName : memberDefinition" ─────────────────
            if (TryParseColonMember(line, GetOrCreateNode))
                continue;
        }

        return builder.Build();
    }

    // ── Direction ─────────────────────────────────────────────────────────────

    private static LayoutDirection ParseDirection(string line)
    {
        var lower = line.ToLowerInvariant();
        if (lower.Contains(" lr", StringComparison.Ordinal)) return LayoutDirection.LeftToRight;
        if (lower.Contains(" rl", StringComparison.Ordinal)) return LayoutDirection.RightToLeft;
        if (lower.Contains(" bt", StringComparison.Ordinal)) return LayoutDirection.BottomToTop;
        return LayoutDirection.TopToBottom;
    }

    // ── Class declaration parsing ─────────────────────────────────────────────

    /// <summary>
    /// Parses the part after "class ": returns (id, optionalLabel).
    /// Handles both plain IDs and <c>ClassName["A label"]</c> form.
    /// </summary>
    private static (string Id, string? Label) ParseClassDeclaration(string token)
    {
        token = token.Trim();
        int bracketStart = token.IndexOf('[');
        if (bracketStart > 0 && token.EndsWith(']'))
        {
            var id = token[..bracketStart].Trim();
            var labelRaw = token[(bracketStart + 1)..^1].Trim();
            // Strip surrounding quotes that Mermaid allows: ["My Label"]
            if (labelRaw.Length >= 2 && labelRaw[0] == '"' && labelRaw[^1] == '"')
                labelRaw = labelRaw[1..^1];
            return (id, labelRaw.Length > 0 ? labelRaw : null);
        }

        return (token, null);
    }

    // ── Member handling ───────────────────────────────────────────────────────

    /// <summary>
    /// Adds a single member line to the appropriate compartment of <paramref name="node"/>.
    /// Methods (those containing <c>()</c>) go to the "methods" compartment;
    /// everything else goes to the "attributes" compartment.
    /// </summary>
    private static void AddMemberToNode(Node node, string memberText)
    {
        if (string.IsNullOrWhiteSpace(memberText))
            return;

        memberText = memberText.Trim();
        bool isMethod = memberText.Contains('(');
        var kind = isMethod ? "methods" : "attributes";

        var compartment = node.Compartments.FirstOrDefault(c => c.Kind == kind);
        if (compartment is null)
        {
            compartment = new NodeCompartment(kind);
            node.Compartments.Add(compartment);
        }

        compartment.Lines.Add(new Label(memberText));
    }

    // ── Colon-member syntax: "ClassName : member" ────────────────────────────

    private static bool TryParseColonMember(string line, Func<string, Node> getOrCreate)
    {
        int colonIdx = line.IndexOf(':');
        if (colonIdx <= 0)
            return false;

        var id = line[..colonIdx].Trim();
        var member = line[(colonIdx + 1)..].Trim();

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(member) || !IsValidClassId(id))
            return false;

        AddMemberToNode(getOrCreate(id), member);
        return true;
    }

    // ── Relationship parsing ──────────────────────────────────────────────────

    private static bool TryParseRelationship(
        string line,
        IDiagramSemanticModelBuilder builder,
        Func<string, Node> getOrCreate)
    {
        var found = FindRelationshipOp(line);
        if (found is null)
            return false;

        var (token, relType, isReversed, opIdx) = found.Value;

        var leftRaw = line[..opIdx].Trim();
        var rightRaw = line[(opIdx + token.Length)..].Trim();

        // Extract relationship label: "ClassName : label" after the right class
        string? label = null;
        int labelColon = rightRaw.LastIndexOf(':');
        if (labelColon >= 0)
        {
            var candidate = rightRaw[(labelColon + 1)..].Trim();
            if (!string.IsNullOrEmpty(candidate))
            {
                label = candidate;
                rightRaw = rightRaw[..labelColon].Trim();
            }
        }

        // Strip cardinality tokens (quoted strings adjacent to class names)
        var leftId = ExtractClassId(leftRaw);
        var rightId = ExtractClassId(rightRaw);

        if (string.IsNullOrEmpty(leftId) || string.IsNullOrEmpty(rightId))
            return false;

        getOrCreate(leftId);
        getOrCreate(rightId);

        // When isReversed=true the semantic "from" end is on the right side of the operator
        // (e.g. "Animal <|-- Dog": Dog IS the child/source, Animal is the parent/target).
        // Swap so that SourceId always represents the logical origin of the relationship.
        string sourceId = isReversed ? rightId : leftId;
        string targetId = isReversed ? leftId : rightId;

        var edge = new Edge(sourceId, targetId);
        if (label is not null)
            edge.Label = new Label(label);

        bool isDashed = token.Contains('.');
        edge.LineStyle = isDashed ? EdgeLineStyle.Dashed : EdgeLineStyle.Solid;
        edge.ArrowHead = MapArrowHead(relType);
        edge.Metadata["class:relationshipType"] = relType;
        edge.Metadata["class:operatorReversed"] = isReversed;

        builder.AddEdge(edge);
        return true;
    }

    /// <summary>
    /// Extracts the class name from an endpoint token, stripping any adjacent
    /// quoted cardinality strings (e.g., <c>Animal "1"</c> → <c>Animal</c>;
    /// <c>"0..*" Zoo</c> → <c>Zoo</c>).
    /// </summary>
    private static string ExtractClassId(string part)
    {
        part = part.Trim();

        // Leading quoted cardinality: "0..*" Zoo
        if (part.StartsWith('"'))
        {
            int end = part.IndexOf('"', 1);
            if (end >= 0)
                part = part[(end + 1)..].Trim();
        }

        // Trailing quoted cardinality: Animal "1"
        if (part.EndsWith('"'))
        {
            int start = part.LastIndexOf('"', part.Length - 2);
            if (start >= 0)
                part = part[..start].Trim();
        }

        return part;
    }

    private static ArrowHeadStyle MapArrowHead(string relType) =>
        relType switch
        {
            "association" or "dependency" => ArrowHeadStyle.Arrow,
            // Composition and aggregation use a diamond marker shape in UML, but the
            // current renderer has no distinct diamond marker. Use None here and rely on
            // class:relationshipType metadata for future renderer support.
            _ => ArrowHeadStyle.None,
        };

    // ── Operator lookup ───────────────────────────────────────────────────────

    private static (string Token, string RelType, bool IsReversed, int Index)?
        FindRelationshipOp(string line)
    {
        (string Token, string RelType, bool IsReversed, int Index)? best = null;

        foreach (var (token, relType, isReversed) in RelOperators)
        {
            int idx = line.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0)
                continue;

            if (best is null
                || idx < best.Value.Index
                || (idx == best.Value.Index && token.Length > best.Value.Token.Length))
            {
                best = (token, relType, isReversed, idx);
            }
        }

        return best;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="s"/> looks like a valid
    /// Mermaid class identifier (starts with a letter or underscore, contains only
    /// letters, digits, or underscores).
    /// </summary>
    private static bool IsValidClassId(string s) =>
        s.Length > 0
        && (char.IsLetter(s[0]) || s[0] == '_')
        && s.All(c => char.IsLetterOrDigit(c) || c == '_');
}
