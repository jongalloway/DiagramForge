using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidSequenceParser : IMermaidDiagramParser
{
    // Message operators in priority order: longest (most specific) first so that
    // "-->>" is matched before "-->" and "->>" is matched before "->".
    private static readonly (string Op, EdgeLineStyle Line, ArrowHeadStyle Arrow)[] MessageOperators =
    [
        ("-->>", EdgeLineStyle.Dashed, ArrowHeadStyle.Arrow),
        ("->>",  EdgeLineStyle.Solid,  ArrowHeadStyle.Arrow),
        ("-->",  EdgeLineStyle.Dashed, ArrowHeadStyle.None),
        ("->",   EdgeLineStyle.Solid,  ArrowHeadStyle.None),
    ];

    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.SequenceDiagram;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("sequencediagram");

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });

        var participants = new Dictionary<string, Node>(StringComparer.Ordinal);
        int participantIndex = 0;
        int messageIndex = 0;
        bool autonumber = false;
        int autonumberIndex = 1;

        // Stack for open rect blocks: each entry holds (group, startMessageIndex).
        var openRects = new Stack<(Group Group, int StartIndex)>();
        int rectIndex = 0;

        Node GetOrCreateParticipant(string id)
        {
            if (!participants.TryGetValue(id, out var node))
            {
                node = new Node(id, id) { Shape = Shape.Rectangle };
                node.Metadata["sequence:participantIndex"] = participantIndex++;
                participants[id] = node;
                builder.AddNode(node);
            }
            return node;
        }

        for (int i = 1; i < document.Lines.Length; i++)
        {
            var line = document.Lines[i];

            // autonumber — enable numbered badges on each message
            if (line.Equals("autonumber", StringComparison.OrdinalIgnoreCase))
            {
                autonumber = true;
                continue;
            }

            // title: <text>  — diagram title directive
            if (line.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
            {
                var titleText = line["title:".Length..].Trim().Trim('"');
                if (!string.IsNullOrEmpty(titleText))
                    builder.WithTitle(titleText);
                continue;
            }

            // subtitle: <text>  — diagram subtitle directive
            if (line.StartsWith("subtitle:", StringComparison.OrdinalIgnoreCase))
            {
                var subtitleText = line["subtitle:".Length..].Trim().Trim('"');
                if (!string.IsNullOrEmpty(subtitleText))
                    builder.WithSubtitle(subtitleText);
                continue;
            }

            // participant ID
            // participant ID as Alias
            if (line.StartsWith("participant ", StringComparison.OrdinalIgnoreCase))
            {
                var rest = line["participant ".Length..].Trim();
                string id, label;

                var asIndex = rest.IndexOf(" as ", StringComparison.OrdinalIgnoreCase);
                if (asIndex >= 0)
                {
                    id = rest[..asIndex].Trim();
                    label = rest[(asIndex + " as ".Length)..].Trim();
                }
                else
                {
                    id = rest;
                    label = rest;
                }

                if (string.IsNullOrEmpty(id))
                    continue;

                var node = GetOrCreateParticipant(id);
                node.Label = new Label(label);
                continue;
            }

            // rect rgb(...) / rect rgba(...)  — colored background band
            if (line.StartsWith("rect ", StringComparison.OrdinalIgnoreCase))
            {
                var colorSpec = line["rect ".Length..].Trim();
                if (TryParseRectColor(colorSpec, out var normalizedColor))
                {
                    var groupId = $"sequence:rect:{rectIndex++:D4}";
                    var group = new Group(groupId, string.Empty);
                    group.FillColor = normalizedColor;
                    group.Metadata["sequence:rectGroup"] = true;
                    openRects.Push((group, messageIndex));
                }
                continue;
            }

            // end  — closes the innermost open rect block
            if (string.Equals(line, "end", StringComparison.OrdinalIgnoreCase) && openRects.Count > 0)
            {
                var (group, startIdx) = openRects.Pop();
                int endIdx = messageIndex - 1;
                if (endIdx >= startIdx)
                {
                    group.Metadata["sequence:rectStartIndex"] = startIdx;
                    group.Metadata["sequence:rectEndIndex"] = endIdx;
                    builder.AddGroup(group);
                }
                continue;
            }

            // Message line:  SourceId->>TargetId: label text
            if (TryParseMessage(line, out var srcId, out var tgtId, out var msgLabel,
                                out var lineStyle, out var arrowHead))
            {
                GetOrCreateParticipant(srcId!);
                GetOrCreateParticipant(tgtId!);

                var edge = new Edge(srcId!, tgtId!)
                {
                    LineStyle = lineStyle,
                    ArrowHead = arrowHead,
                };
                edge.Metadata["sequence:messageIndex"] = messageIndex++;

                if (autonumber)
                    edge.Metadata["sequence:autonumberIndex"] = autonumberIndex++;

                if (!string.IsNullOrEmpty(msgLabel))
                    edge.Label = new Label(msgLabel!);

                builder.AddEdge(edge);
            }
        }

        var diagram = builder.Build();
        if (autonumber)
            diagram.Metadata["sequence:autonumber"] = true;
        return diagram;
    }

    /// <summary>
    /// Validates and normalises a rect color specification of the form
    /// <c>rgb(R,G,B)</c> or <c>rgba(R,G,B,A)</c>.
    /// Returns <see langword="false"/> when the input does not match either form.
    /// Only the structural prefix (<c>rgb(</c> / <c>rgba(</c>) and closing
    /// parenthesis are validated; individual component values are passed through as-is.
    /// </summary>
    private static bool TryParseRectColor(string colorSpec, out string normalizedColor)
    {
        normalizedColor = string.Empty;
        var trimmed = colorSpec.Trim();

        bool isRgb  = trimmed.StartsWith("rgb(",  StringComparison.OrdinalIgnoreCase);
        bool isRgba = trimmed.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase);
        if (!isRgb && !isRgba)
            return false;

        if (!trimmed.EndsWith(")"))
            return false;

        normalizedColor = trimmed;
        return true;
    }

    /// <summary>
    /// Attempts to parse a sequence message line of the form
    /// <c>SourceId{op}TargetId: label</c> where {op} is one of the
    /// recognised arrow operators.
    /// </summary>
    private static bool TryParseMessage(
        string line,
        out string? srcId,
        out string? tgtId,
        out string? label,
        out EdgeLineStyle lineStyle,
        out ArrowHeadStyle arrowHead)
    {
        srcId = tgtId = label = null;
        lineStyle = EdgeLineStyle.Solid;
        arrowHead = ArrowHeadStyle.Arrow;

        // Find the leftmost operator match; prefer longer operator on a position tie.
        string? bestOp = null;
        int bestIdx = -1;
        EdgeLineStyle bestLine = EdgeLineStyle.Solid;
        ArrowHeadStyle bestArrow = ArrowHeadStyle.Arrow;

        foreach (var (op, opLine, opArrow) in MessageOperators)
        {
            int idx = line.IndexOf(op, StringComparison.Ordinal);
            if (idx < 0)
                continue;

            if (bestIdx < 0 || idx < bestIdx || (idx == bestIdx && op.Length > bestOp!.Length))
            {
                bestIdx = idx;
                bestOp = op;
                bestLine = opLine;
                bestArrow = opArrow;
            }
        }

        if (bestOp is null)
            return false;

        var left = line[..bestIdx].Trim();
        var right = line[(bestIdx + bestOp.Length)..].Trim();

        int colonIdx = right.IndexOf(':', StringComparison.Ordinal);
        string target;
        if (colonIdx >= 0)
        {
            target = right[..colonIdx].Trim();
            label = right[(colonIdx + 1)..].Trim();
            if (string.IsNullOrEmpty(label))
                label = null;
        }
        else
        {
            target = right;
        }

        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(target))
            return false;

        srcId = left;
        tgtId = target;
        lineStyle = bestLine;
        arrowHead = bestArrow;
        return true;
    }
}
