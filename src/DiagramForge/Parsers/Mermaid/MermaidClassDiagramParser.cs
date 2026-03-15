using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidClassDiagramParser : IMermaidDiagramParser
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
        var groupStack = new Stack<GroupFrame>();
        var namespaceIdsSeen = new HashSet<string>(StringComparer.Ordinal);
        int autoNamespaceId = 0;

        Node GetOrCreateNode(string id)
        {
            if (!nodesSeen.TryGetValue(id, out var node))
            {
                node = new Node(id, id) { Shape = Shape.Rectangle };
                node.Metadata["class:isClass"] = true;
                nodesSeen[id] = node;
                builder.AddNode(node);
            }

            foreach (var frame in groupStack)
                frame.Members.Add(id);

            return node;
        }

        void CloseNamespace()
        {
            var frame = groupStack.Pop();
            frame.Group.ChildNodeIds.AddRange(frame.Members.OrderBy(member => member, StringComparer.Ordinal));

            if (groupStack.Count > 0)
                groupStack.Peek().Group.ChildGroupIds.Add(frame.Group.Id);

            builder.AddGroup(frame.Group);
        }

        // Current class whose {} block is being parsed (null = top-level).
        Node? currentBlockNode = null;

        for (int i = 1; i < document.Lines.Length; i++)
        {
            var line = document.Lines[i];

            // ── Close brace ends a member block ─────────────────────────────
            if (line == "}")
            {
                if (currentBlockNode is not null)
                {
                    currentBlockNode = null;
                }
                else if (groupStack.Count > 0)
                {
                    CloseNamespace();
                }

                continue;
            }

            // ── Lines inside a member block ──────────────────────────────────
            if (currentBlockNode is not null)
            {
                if (TryParseStandaloneAnnotation(line, out var blockAnnotation))
                {
                    AddAnnotation(currentBlockNode, blockAnnotation);
                    continue;
                }

                AddMemberToNode(currentBlockNode, line);
                continue;
            }

            // ── namespace ────────────────────────────────────────────────────
            if (TryParseNamespaceStart(line, out var namespaceTitle))
            {
                string namespaceId = CreateNamespaceId(namespaceTitle, namespaceIdsSeen, ref autoNamespaceId);
                groupStack.Push(new GroupFrame(new Group(namespaceId, namespaceTitle)));
                continue;
            }

            // ── direction ────────────────────────────────────────────────────
            if (line.StartsWith("direction ", StringComparison.OrdinalIgnoreCase))
            {
                hints.Direction = ParseDirection(line);
                continue;
            }

            // ── Separate annotation line: "<<interface>> Shape" ────────────
            if (TryParseAttachedAnnotation(line, out var annotationText, out var annotatedClassId))
            {
                AddAnnotation(GetOrCreateNode(annotatedClassId), annotationText);
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

                var (id, label, inlineAnnotation) = ParseClassDeclaration(rest);
                if (!string.IsNullOrEmpty(id))
                {
                    var node = GetOrCreateNode(id);
                    if (label is not null)
                        node.Label = new Label(label);

                    if (inlineAnnotation is not null)
                        AddAnnotation(node, inlineAnnotation);

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

        while (groupStack.Count > 0)
            CloseNamespace();

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
    /// Parses the part after "class ": returns (id, optionalLabel, inlineAnnotation).
    /// Handles both plain IDs and <c>ClassName["A label"]</c> form.
    /// </summary>
    private static (string Id, string? Label, string? Annotation) ParseClassDeclaration(string token)
    {
        token = token.Trim();
        string? annotation = null;

        if (TryExtractTrailingAnnotation(ref token, out var inlineAnnotation))
            annotation = inlineAnnotation;

        int bracketStart = token.IndexOf('[');
        if (bracketStart > 0 && token.EndsWith(']'))
        {
            var id = NormalizeClassId(token[..bracketStart].Trim());
            var labelRaw = token[(bracketStart + 1)..^1].Trim();
            // Strip surrounding quotes that Mermaid allows: ["My Label"]
            if (labelRaw.Length >= 2 && labelRaw[0] == '"' && labelRaw[^1] == '"')
                labelRaw = labelRaw[1..^1];
            return (id, labelRaw.Length > 0 ? labelRaw : null, annotation);
        }

        token = NormalizeClassId(token);
        return (token, null, annotation);
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

        var leftEndpoint = ParseEndpoint(leftRaw);
        var rightEndpoint = ParseEndpoint(rightRaw);

        var leftId = leftEndpoint.ClassId;
        var rightId = rightEndpoint.ClassId;

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

        string? sourceMultiplicity = isReversed
            ? rightEndpoint.TrailingCardinality ?? rightEndpoint.LeadingCardinality
            : leftEndpoint.TrailingCardinality ?? leftEndpoint.LeadingCardinality;
        string? targetMultiplicity = isReversed
            ? leftEndpoint.LeadingCardinality ?? leftEndpoint.TrailingCardinality
            : rightEndpoint.LeadingCardinality ?? rightEndpoint.TrailingCardinality;

        if (sourceMultiplicity is not null)
            edge.SourceLabel = new Label(sourceMultiplicity);

        if (targetMultiplicity is not null)
            edge.TargetLabel = new Label(targetMultiplicity);

        bool isDashed = token.Contains('.');
        edge.LineStyle = isDashed ? EdgeLineStyle.Dashed : EdgeLineStyle.Solid;
        edge.ArrowHead = MapTargetArrowHead(relType);
        edge.SourceArrowHead = MapSourceArrowHead(relType);
        edge.Metadata["class:relationshipType"] = relType;
        edge.Metadata["class:operatorReversed"] = isReversed;

        builder.AddEdge(edge);
        return true;
    }

    private static (string ClassId, string? LeadingCardinality, string? TrailingCardinality) ParseEndpoint(string part)
    {
        part = part.Trim();

        var leading = TryExtractLeadingQuoted(ref part);
        var trailing = TryExtractTrailingQuoted(ref part);

        return (NormalizeClassId(part), leading, trailing);
    }

    private static string? TryExtractLeadingQuoted(ref string part)
    {
        if (!part.StartsWith('"'))
            return null;

        int end = part.IndexOf('"', 1);
        if (end < 0)
            return null;

        string cardinality = part[1..end].Trim();
        part = part[(end + 1)..].Trim();
        return cardinality.Length == 0 ? null : cardinality;
    }

    private static string? TryExtractTrailingQuoted(ref string part)
    {
        if (!part.EndsWith('"'))
            return null;

        int start = part.LastIndexOf('"', part.Length - 2);
        if (start < 0)
            return null;

        string cardinality = part[(start + 1)..^1].Trim();
        part = part[..start].Trim();
        return cardinality.Length == 0 ? null : cardinality;
    }

    private static ArrowHeadStyle MapTargetArrowHead(string relType) =>
        relType switch
        {
            "association" or "dependency" => ArrowHeadStyle.Arrow,
            "inheritance" or "realization" => ArrowHeadStyle.OpenArrow,
            _ => ArrowHeadStyle.None,
        };

    private static ArrowHeadStyle MapSourceArrowHead(string relType) =>
        relType switch
        {
            "composition" => ArrowHeadStyle.Diamond,
            "aggregation" => ArrowHeadStyle.Circle,
            _ => ArrowHeadStyle.None,
        };

    // ── Operator lookup ───────────────────────────────────────────────────────

    private static (string Token, string RelType, bool IsReversed, int Index)?
        FindRelationshipOp(string line)
    {
        (string Token, string RelType, bool IsReversed, int Index)? best = null;

        foreach (var (token, relType, isReversed) in RelOperators)
        {
            foreach (int idx in FindTokenIndicesOutsideQuotes(line, token))
            {
                if (best is null
                    || idx < best.Value.Index
                    || (idx == best.Value.Index && token.Length > best.Value.Token.Length))
                {
                    best = (token, relType, isReversed, idx);
                }
            }
        }

        return best;
    }

    private static IEnumerable<int> FindTokenIndicesOutsideQuotes(string line, string token)
    {
        bool inQuotes = false;
        for (int i = 0; i <= line.Length - token.Length; i++)
        {
            if (line[i] == '"')
                inQuotes = !inQuotes;

            if (inQuotes)
                continue;

            if (string.Compare(line, i, token, 0, token.Length, StringComparison.Ordinal) == 0)
                yield return i;
        }
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

    private static bool TryParseNamespaceStart(string line, out string namespaceTitle)
    {
        namespaceTitle = string.Empty;
        if (!line.StartsWith("namespace ", StringComparison.OrdinalIgnoreCase) || !line.EndsWith('{'))
            return false;

        namespaceTitle = line[10..^1].Trim();
        return namespaceTitle.Length > 0;
    }

    private static string CreateNamespaceId(string namespaceTitle, HashSet<string> idsSeen, ref int autoNamespaceId)
    {
        string candidate = $"namespace:{namespaceTitle}";
        while (!idsSeen.Add(candidate))
            candidate = $"namespace:{namespaceTitle}:{autoNamespaceId++}";
        return candidate;
    }

    private static bool TryExtractTrailingAnnotation(ref string token, out string annotation)
    {
        annotation = string.Empty;
        if (!token.EndsWith(">>", StringComparison.Ordinal))
            return false;

        int start = token.LastIndexOf("<<", StringComparison.Ordinal);
        if (start <= 0)
            return false;

        annotation = token[(start + 2)..^2].Trim();
        if (annotation.Length == 0)
            return false;

        token = token[..start].TrimEnd();
        return true;
    }

    private static bool TryParseStandaloneAnnotation(string line, out string annotation)
    {
        annotation = string.Empty;
        line = line.Trim();
        if (!line.StartsWith("<<", StringComparison.Ordinal) || !line.EndsWith(">>", StringComparison.Ordinal))
            return false;

        annotation = line[2..^2].Trim();
        return annotation.Length > 0;
    }

    private static bool TryParseAttachedAnnotation(string line, out string annotation, out string classId)
    {
        annotation = string.Empty;
        classId = string.Empty;

        line = line.Trim();
        if (!line.StartsWith("<<", StringComparison.Ordinal))
            return false;

        int end = line.IndexOf(">>", StringComparison.Ordinal);
        if (end < 0)
            return false;

        annotation = line[2..end].Trim();
        classId = NormalizeClassId(line[(end + 2)..].Trim());
        return annotation.Length > 0 && IsValidClassId(classId);
    }

    private static void AddAnnotation(Node node, string annotation)
    {
        if (node.Annotations.Any(existing => string.Equals(existing.Text, annotation, StringComparison.Ordinal)))
            return;

        node.Annotations.Add(new Label(annotation));
    }

    private static string NormalizeClassId(string token)
    {
        token = token.Trim().Trim('`');
        int genericStart = token.IndexOf('~');
        if (genericStart > 0)
            token = token[..genericStart];
        return token;
    }
}
