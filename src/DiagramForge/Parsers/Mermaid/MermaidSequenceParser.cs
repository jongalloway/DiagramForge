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

    // Control-flow block keywords (order: longest first is not required here but kept for clarity).
    private static readonly string[] CfKeywords = ["loop", "alt", "par", "critical", "break"];

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

        // Unified stack for open blocks (rect and control-flow); each entry holds (group, startMessageIndex).
        var openBlocks = new Stack<(Group Group, int StartIndex)>();
        int blockIndex = 0;
        int noteIndex = 0;

        // Per-participant stack of open activation bars: each entry holds (group, startMessageIndex).
        var openActivations = new Dictionary<string, Stack<(Group Group, int StartIndex)>>(StringComparer.Ordinal);
        int activationIndex = 0;

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

        void ActivateParticipant(string id, int startIdx)
        {
            GetOrCreateParticipant(id);
            if (!openActivations.TryGetValue(id, out var stack))
            {
                stack = new Stack<(Group, int)>();
                openActivations[id] = stack;
            }
            int level = stack.Count;
            var groupId = $"sequence:activation:{activationIndex++:D4}";
            var group = new Group(groupId, string.Empty);
            group.Metadata["sequence:activationGroup"] = true;
            group.Metadata["sequence:activationParticipant"] = id;
            group.Metadata["sequence:activationLevel"] = level;
            stack.Push((group, startIdx));
        }

        void DeactivateParticipant(string id, int endIdx)
        {
            if (!openActivations.TryGetValue(id, out var stack) || stack.Count == 0)
                return;
            var (group, startIdx) = stack.Pop();
            if (endIdx >= startIdx)
            {
                group.Metadata["sequence:activationStartIndex"] = startIdx;
                group.Metadata["sequence:activationEndIndex"] = endIdx;
                builder.AddGroup(group);
            }
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
            // participant ID@{ icon: 'pack:name' }
            // participant ID as Alias @{ icon: 'pack:name' }
            if (line.StartsWith("participant ", StringComparison.OrdinalIgnoreCase))
            {
                var rest = line["participant ".Length..].Trim();
                string id, label;
                string? iconRef = null;

                // Extract @{ ... } metadata block if present.
                int atBraceIdx = rest.IndexOf("@{", StringComparison.Ordinal);
                if (atBraceIdx >= 0)
                {
                    iconRef = TryParseIconFromMetaBlock(rest[atBraceIdx..]);
                    rest = rest[..atBraceIdx].Trim();
                }

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
                if (iconRef is not null)
                    node.IconRef = iconRef;
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

            // activate X  — explicit activation bar start
            if (line.StartsWith("activate ", StringComparison.OrdinalIgnoreCase))
            {
                var id = line["activate ".Length..].Trim();
                if (!string.IsNullOrEmpty(id))
                    ActivateParticipant(id, messageIndex);
                continue;
            }

            // deactivate X  — explicit activation bar end
            if (line.StartsWith("deactivate ", StringComparison.OrdinalIgnoreCase))
            {
                var id = line["deactivate ".Length..].Trim();
                if (!string.IsNullOrEmpty(id))
                    DeactivateParticipant(id, messageIndex - 1);
                continue;
            }

            // rect rgb(...) / rect rgba(...)  — colored background band
            if (line.StartsWith("rect ", StringComparison.OrdinalIgnoreCase))
            {
                var colorSpec = line["rect ".Length..].Trim();
                if (TryParseRectColor(colorSpec, out var normalizedColor))
                {
                    var groupId = $"sequence:rect:{blockIndex++:D4}";
                    var group = new Group(groupId, string.Empty);
                    group.FillColor = normalizedColor;
                    group.Metadata["sequence:rectGroup"] = true;
                    openBlocks.Push((group, messageIndex));
                }
                continue;
            }

            // Control-flow blocks: loop/alt/par/critical/break <label>
            if (TryParseCfKeyword(line, out var cfKind, out var cfLabel))
            {
                var groupId = $"sequence:cf:{blockIndex++:D4}";
                var group = new Group(groupId, cfLabel ?? string.Empty);
                group.Metadata["sequence:cfGroup"] = true;
                group.Metadata["sequence:cfKind"] = cfKind!;
                openBlocks.Push((group, messageIndex));
                continue;
            }

            // Separator keywords inside control-flow blocks (else/and/option); skip without closing.
            if (IsCfSeparator(line))
                continue;

            // end  — closes the innermost open block (rect or control-flow)
            if (string.Equals(line, "end", StringComparison.OrdinalIgnoreCase) && openBlocks.Count > 0)
            {
                var (group, startIdx) = openBlocks.Pop();
                int endIdx = messageIndex - 1;
                if (endIdx >= startIdx)
                {
                    bool isRect = group.Metadata.TryGetValue("sequence:rectGroup", out var rObj) && rObj is true;
                    if (isRect)
                    {
                        group.Metadata["sequence:rectStartIndex"] = startIdx;
                        group.Metadata["sequence:rectEndIndex"] = endIdx;
                    }
                    else
                    {
                        group.Metadata["sequence:cfStartIndex"] = startIdx;
                        group.Metadata["sequence:cfEndIndex"] = endIdx;
                    }
                    builder.AddGroup(group);
                }
                continue;
            }

            // Message line:  SourceId->>TargetId: label text
            // Targets may carry a leading '+' (activate target) or '-' (deactivate source).
            if (TryParseMessage(line, out var srcId, out var tgtId, out var msgLabel,
                                out var lineStyle, out var arrowHead))
            {
                char activationMod = '\0';
                if (tgtId!.Length > 1 && tgtId[0] == '+')
                {
                    activationMod = '+';
                    tgtId = tgtId[1..];
                }
                else if (tgtId!.Length > 1 && tgtId[0] == '-')
                {
                    activationMod = '-';
                    tgtId = tgtId[1..];
                }

                GetOrCreateParticipant(srcId!);
                GetOrCreateParticipant(tgtId);

                int currentMsgIdx = messageIndex;
                var edge = new Edge(srcId!, tgtId)
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

                // Handle activation shorthand after edge is registered.
                if (activationMod == '+')
                    ActivateParticipant(tgtId, currentMsgIdx);
                else if (activationMod == '-')
                    DeactivateParticipant(srcId!, currentMsgIdx);
            }
        }

        // Auto-close any activation bars that were opened but never explicitly deactivated.
        // This mirrors Mermaid's behaviour: an unclosed activate extends to the end of the diagram.
        int lastMsgIdx = messageIndex - 1;
        foreach (var stack in openActivations.Values)
        {
            while (stack.Count > 0)
            {
                var (group, startIdx) = stack.Pop();
                if (lastMsgIdx >= startIdx)
                {
                    group.Metadata["sequence:activationStartIndex"] = startIdx;
                    group.Metadata["sequence:activationEndIndex"] = lastMsgIdx;
                    builder.AddGroup(group);
                }
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
            || line.StartsWith("activate ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("deactivate ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("title:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("subtitle:", StringComparison.OrdinalIgnoreCase)
            || line.Equals("autonumber", StringComparison.OrdinalIgnoreCase)
            || line.Equals("end", StringComparison.OrdinalIgnoreCase)
            || TryParseMessage(line, out _, out _, out _, out _, out _);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="line"/> is a control-flow
    /// separator keyword (<c>else</c>, <c>and</c>, or <c>option</c>), optionally
    /// followed by a label.  These keywords delimit sections inside a combined
    /// fragment but do not open or close a block.
    /// </summary>
    private static bool IsCfSeparator(string line)
    {
        return line.Equals("else",   StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("else ",   StringComparison.OrdinalIgnoreCase)
            || line.Equals("and",    StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("and ",    StringComparison.OrdinalIgnoreCase)
            || line.Equals("option", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("option ", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tries to parse a control-flow block keyword (<c>loop</c>, <c>alt</c>,
    /// <c>par</c>, <c>critical</c>, or <c>break</c>) with an optional label.
    /// Returns <see langword="true"/> and sets <paramref name="kind"/> and
    /// <paramref name="label"/> on success.
    /// </summary>
    private static bool TryParseCfKeyword(string line, out string? kind, out string? label)
    {
        kind = label = null;

        foreach (var kw in CfKeywords)
        {
            if (line.Equals(kw, StringComparison.OrdinalIgnoreCase))
            {
                kind = kw;
                label = string.Empty;
                return true;
            }

            if (line.StartsWith(kw + " ", StringComparison.OrdinalIgnoreCase))
            {
                kind = kw;
                label = line[(kw.Length + 1)..].Trim();
                return true;
            }
        }

        return false;
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
    /// Attempts to extract an icon reference from a Mermaid metadata block of the form
    /// <c>@{ icon: 'pack:name' }</c> or <c>@{ icon: "pack:name" }</c>.
    /// Returns <see langword="null"/> when the block is absent or malformed.
    /// </summary>
    private static string? TryParseIconFromMetaBlock(string block)
    {
        int open = block.IndexOf('{');
        int close = block.LastIndexOf('}');
        if (open < 0 || close <= open)
            return null;

        var content = block[(open + 1)..close];

        // Walk key/value pairs so that "icon:" is matched only as a full key, not as a
        // substring of another key (e.g. "bicon:") or inside a quoted string value.
        int i = 0;

        while (i < content.Length)
        {
            while (i < content.Length && (char.IsWhiteSpace(content[i]) || content[i] == ','))
                i++;

            if (i >= content.Length)
                break;

            int keyStart = i;
            while (i < content.Length && !char.IsWhiteSpace(content[i]) && content[i] != ':' && content[i] != ',')
                i++;

            if (i == keyStart)
                return null;

            var key = content[keyStart..i];

            while (i < content.Length && char.IsWhiteSpace(content[i]))
                i++;

            if (i >= content.Length || content[i] != ':')
                return null;

            i++; // consume ':'

            while (i < content.Length && char.IsWhiteSpace(content[i]))
                i++;

            if (string.Equals(key, "icon", StringComparison.OrdinalIgnoreCase))
            {
                if (i >= content.Length || (content[i] != '\'' && content[i] != '"'))
                    return null;

                char quote = content[i];
                int valueStart = ++i;

                while (i < content.Length && content[i] != quote)
                    i++;

                if (i >= content.Length)
                    return null;

                var iconValue = content[valueStart..i];
                if (string.IsNullOrEmpty(iconValue))
                    return null;

                // Validate pack:name format — must have a colon with non-empty pack and name.
                int colonIdx = iconValue.IndexOf(':');
                if (colonIdx <= 0 || colonIdx >= iconValue.Length - 1)
                    return null;

                return iconValue;
            }

            // Skip the value for any non-icon key.
            if (i < content.Length && (content[i] == '\'' || content[i] == '"'))
            {
                char quote = content[i++];
                while (i < content.Length && content[i] != quote)
                    i++;

                if (i >= content.Length)
                    return null;

                i++; // consume closing quote
            }
            else
            {
                while (i < content.Length && content[i] != ',')
                    i++;
            }
        }

        return null;
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
