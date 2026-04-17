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
        int noteIndex = 0;

        // Tracks the most recently parsed note group so that subsequent indented
        // lines can be appended as continuation text (multi-line notes).
        Group? pendingNoteGroup = null;

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

            // Multi-line note continuation: if the raw line is indented and no
            // other keyword matches, append to the pending note.
            if (pendingNoteGroup != null)
            {
                bool isIndented = i < document.RawLines.Length
                    && document.RawLines[i].Length > 0
                    && char.IsWhiteSpace(document.RawLines[i][0]);

                if (isIndented && !IsSequenceKeyword(line))
                {
                    pendingNoteGroup.Label.Text += "\n" + line;
                    continue;
                }

                pendingNoteGroup = null;
            }

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

            // Note right of X: text
            // Note left of X: text
            // Note over X[,Y]: text
            if (TryParseNote(line, out var notePosition, out var noteP1, out var noteP2, out var noteText))
            {
                GetOrCreateParticipant(noteP1!);
                if (noteP2 is not null)
                    GetOrCreateParticipant(noteP2);

                var groupId = $"sequence:note:{noteIndex++:D4}";
                var group = new Group(groupId, noteText ?? string.Empty);
                group.Metadata["sequence:noteGroup"] = true;
                group.Metadata["sequence:notePosition"] = notePosition!;
                group.Metadata["sequence:noteParticipant"] = noteP1!;
                if (noteP2 is not null)
                    group.Metadata["sequence:noteParticipant2"] = noteP2;
                group.Metadata["sequence:noteSequenceIndex"] = messageIndex++;
                builder.AddGroup(group);
                pendingNoteGroup = group;
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
    /// Returns <see langword="true"/> when <paramref name="line"/> starts with a
    /// known sequence-diagram keyword, which prevents it from being absorbed as a
    /// multi-line note continuation.
    /// </summary>
    private static bool IsSequenceKeyword(string line)
    {
        return line.StartsWith("participant ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Note ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("rect ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("title:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("subtitle:", StringComparison.OrdinalIgnoreCase)
            || line.Equals("autonumber", StringComparison.OrdinalIgnoreCase)
            || line.Equals("end", StringComparison.OrdinalIgnoreCase)
            || TryParseMessage(line, out _, out _, out _, out _, out _);
    }

    /// <summary>
    /// Attempts to parse a note directive of the form
    /// <c>Note right of X: text</c>, <c>Note left of X: text</c>, or
    /// <c>Note over X[,Y]: text</c>.
    /// </summary>
    private static bool TryParseNote(
        string line,
        out string? position,
        out string? participant1,
        out string? participant2,
        out string? text)
    {
        position = participant1 = participant2 = text = null;

        if (!line.StartsWith("Note ", StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = line["Note ".Length..].TrimStart();

        string afterPosition;
        if (rest.StartsWith("right of ", StringComparison.OrdinalIgnoreCase))
        {
            position = "rightOf";
            afterPosition = rest["right of ".Length..];
        }
        else if (rest.StartsWith("left of ", StringComparison.OrdinalIgnoreCase))
        {
            position = "leftOf";
            afterPosition = rest["left of ".Length..];
        }
        else if (rest.StartsWith("over ", StringComparison.OrdinalIgnoreCase))
        {
            position = "over";
            afterPosition = rest["over ".Length..];
        }
        else
        {
            return false;
        }

        ParseNoteParticipantsAndText(afterPosition.Trim(), out participant1, out participant2, out text);
        return participant1 is not null;
    }

    /// <summary>
    /// Splits the participant/text portion of a note directive into its components.
    /// The input has the form <c>P1[,P2]: text</c> where the colon and text are optional.
    /// </summary>
    private static void ParseNoteParticipantsAndText(
        string rest,
        out string? participant1,
        out string? participant2,
        out string? text)
    {
        participant1 = participant2 = text = null;

        int colonIdx = rest.IndexOf(':', StringComparison.Ordinal);
        string participants;
        if (colonIdx >= 0)
        {
            participants = rest[..colonIdx].Trim();
            text = rest[(colonIdx + 1)..].Trim();
            if (string.IsNullOrEmpty(text))
                text = null;
        }
        else
        {
            participants = rest.Trim();
        }

        int commaIdx = participants.IndexOf(',', StringComparison.Ordinal);
        if (commaIdx >= 0)
        {
            participant1 = participants[..commaIdx].Trim();
            participant2 = participants[(commaIdx + 1)..].Trim();
            if (string.IsNullOrEmpty(participant2))
                participant2 = null;
        }
        else
        {
            participant1 = participants.Trim();
        }

        if (string.IsNullOrEmpty(participant1))
            participant1 = null;
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
