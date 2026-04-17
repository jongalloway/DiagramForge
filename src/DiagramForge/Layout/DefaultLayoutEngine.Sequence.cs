using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    // Width of the right-side loopback arc rendered for self-messages.
    // This value is stored in sequence:selfMessageLoopWidth edge metadata so the
    // renderer always reads the same value that the canvas-width calculation uses.
    private const double SelfMessageLoopWidth = 40;

    // Extra horizontal space reserved on the left when autonumber is active so that
    // the numbered circle badge fits between the canvas edge and the first lifeline.
    private const double SequenceAutonumberBadgeExtraLeft = 36;

    // Multiplier applied to ShadowBlur when deriving additional autonumber left clearance.
    // A Gaussian blur of stdDeviation σ is visually significant within ~2σ from the source
    // edge, so multiplying ShadowBlur by 2 gives a conservative estimate of how far the
    // filter region extends beyond the node boundary.
    private const double SequenceAutonumberShadowClearanceMultiplier = 2.0;

    // Horizontal gap between a note box and the adjacent lifeline.
    private const double NoteLifelineGap = 6;

    // Horizontal and vertical padding inside a note box between the box border and text.
    private const double NoteHPad = 8;
    private const double NoteVPad = 5;

    // Minimum note box width and height.
    private const double NoteMinWidth  = 60;
    private const double NoteMinHeight = 28;

    // Width of a single activation bar drawn on a lifeline.
    private const double ActivationBarWidth = 8;

    // Horizontal offset per nesting level so overlapping activation bars remain visible.
    private const double ActivationBarLevelOffset = 4;

    private static void LayoutSequenceDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double hGap,
        double vGap,
        double pad)
    {
        foreach (var node in diagram.Nodes.Values)
            SizeStandardNode(node, theme, minW, nodeH);

        var ordered = diagram.Nodes.Values
            .OrderBy(n => TryGetMetadataInt(n.Metadata, "sequence:participantIndex", out var participantIndex)
                ? participantIndex
                : int.MaxValue)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        // Reserve vertical space for the title and/or subtitle so they don't overlap participants.
        double headingOffset = ComputeHeadingOffset(diagram, theme);

        // Reserve horizontal space for autonumber badges to the left of participants.
        // When node shadows are active the SVG filter extends beyond the node boundary,
        // which can visually encroach on the badge. Add clearance proportional to
        // ShadowBlur so the badge remains visible across all shadow themes.
        bool hasAutonumber = diagram.Metadata.ContainsKey("sequence:autonumber");
        double shadowExtra = theme.UseNodeShadows ? SequenceAutonumberShadowClearanceMultiplier * theme.ShadowBlur : 0.0;
        double autonumberExtraLeft = hasAutonumber ? SequenceAutonumberBadgeExtraLeft + shadowExtra : 0;

        double runX = pad + autonumberExtraLeft;
        double participantStripHeight = ordered.Max(node => node.Height);
        foreach (var node in ordered)
        {
            node.X = runX;
            node.Y = pad + headingOffset + (participantStripHeight - node.Height);
            runX += node.Width + hGap;
        }

        double firstMessageY = pad + headingOffset + participantStripHeight + vGap / 2;
        double messageRowHeight = vGap;

        if (hasAutonumber)
            diagram.Metadata["sequence:autonumberBadgeX"] = pad + autonumberExtraLeft / 2;

        // Build a combined ordered sequence of messages (edges) and notes (note-type groups).
        // Both share a unified sequence index so they interleave correctly.
        var sequenceItems = new List<(int SequenceIdx, bool IsSelf, Edge? Edge, Group? NoteGroup)>();

        foreach (var edge in diagram.Edges)
        {
            if (!TryGetMetadataInt(edge.Metadata, "sequence:messageIndex", out var msgIdx))
                continue;
            bool isSelf = string.Equals(edge.SourceId, edge.TargetId, StringComparison.Ordinal);
            sequenceItems.Add((msgIdx, isSelf, edge, null));
        }

        foreach (var group in diagram.Groups)
        {
            if (!group.Metadata.TryGetValue("sequence:noteGroup", out var isNoteObj) || isNoteObj is not true)
                continue;
            if (!TryGetMetadataInt(group.Metadata, "sequence:noteSequenceIndex", out var noteIdx))
                continue;
            sequenceItems.Add((noteIdx, false, null, group));
        }

        sequenceItems.Sort((a, b) => a.SequenceIdx.CompareTo(b.SequenceIdx));

        // Assign Y coordinates to each item in sequence order.
        // orderedEdges collects only edges for the canvas-width and rect-band passes below.
        var orderedEdges = new List<Edge>(diagram.Edges.Count);
        double runY = firstMessageY;
        foreach (var (_, isSelf, edge, noteGroup) in sequenceItems)
        {
            if (edge != null)
            {
                edge.Metadata["sequence:messageY"] = runY;
                if (isSelf)
                {
                    edge.Metadata["sequence:selfMessage"] = true;
                    edge.Metadata["sequence:selfMessageHeight"] = messageRowHeight;
                    // Store the loop width on the edge so the renderer reads the same
                    // value that the canvas-width calculation uses — no separate constant
                    // to keep in sync between layout and rendering.
                    edge.Metadata["sequence:selfMessageLoopWidth"] = SelfMessageLoopWidth;
                    runY += messageRowHeight * 2;
                }
                else
                {
                    runY += messageRowHeight;
                }
                orderedEdges.Add(edge);
            }
            else if (noteGroup != null)
            {
                noteGroup.Metadata["sequence:noteY"] = runY;
                // Pre-compute note box height so multi-line notes don't overlap the
                // next row. Uses the same formula as LayoutSequenceNotes below.
                double preNoteHeight = ComputeNoteBoxHeight(noteGroup.Label.Text, theme);
                runY += Math.Max(messageRowHeight, preNoteHeight);
            }
        }

        double canvasHeight = runY + pad;
        diagram.Metadata["sequence:canvasHeight"] = canvasHeight;

        // Compute canvas width, extending it when any participant has a self-message
        // so the loopback arc and its label are not clipped at the canvas boundary.
        double maxNodeRight = ordered.Count > 0
            ? ordered.Max(n => n.X + n.Width)
            : 0;
        double extraRight = 0;
        foreach (var edge in orderedEdges)
        {
            if (!edge.Metadata.ContainsKey("sequence:selfMessage"))
                continue;
            if (!diagram.Nodes.TryGetValue(edge.SourceId, out var selfNode))
                continue;
            // The arc is anchored at the lifeline (center X), matching AppendLifelines.
            double lifelineX = selfNode.X + selfNode.Width / 2;
            double arcRight = lifelineX + SelfMessageLoopWidth;
            // With text-anchor="middle" the label is centered at arcRight+6; right
            // edge = arcRight + 6 + labelWidth/2.  Use the same font-size multiple
            // (0.85×) that the renderer applies to edge labels.
            double labelWidth = edge.Label is not null
                ? EstimateTextWidth(edge.Label.Text, theme.FontSize * 0.85)
                : 0;
            double contentRight = arcRight + 6 + labelWidth / 2;
            if (contentRight > maxNodeRight + extraRight)
                extraRight = contentRight - maxNodeRight;
        }
        double canvasWidth = maxNodeRight + extraRight + pad;

        // Position note groups now that participant positions and sequence Y values are final.
        LayoutSequenceNotes(diagram, messageRowHeight, theme, ref canvasWidth);

        // Position rect-band and CF-block groups now that message Y coordinates are final.
        // Both kinds share the same index-range → Y-range logic.
        // CF groups also determine the minimum canvas width needed for their keyword tab.
        double cfTabFontSize = theme.FontSize * 0.80;
        foreach (var group in diagram.Groups)
        {
            bool isRect = group.Metadata.TryGetValue("sequence:rectGroup", out var isRectObj) && isRectObj is true;
            bool isCf   = group.Metadata.TryGetValue("sequence:cfGroup",   out var isCfObj)   && isCfObj   is true;
            if (!isRect && !isCf)
                continue;

            string startKey = isRect ? "sequence:rectStartIndex" : "sequence:cfStartIndex";
            string endKey   = isRect ? "sequence:rectEndIndex"   : "sequence:cfEndIndex";

            if (!group.Metadata.TryGetValue(startKey, out var startIdxObj)
                || !group.Metadata.TryGetValue(endKey, out var endIdxObj))
                continue;

            int startIdx = Convert.ToInt32(startIdxObj, System.Globalization.CultureInfo.InvariantCulture);
            int endIdx   = Convert.ToInt32(endIdxObj,   System.Globalization.CultureInfo.InvariantCulture);

            double halfRow    = messageRowHeight / 2;
            double bandTop    = double.MaxValue;
            double bandBottom = double.MinValue;

            foreach (var e in orderedEdges)
            {
                if (!TryGetMetadataInt(e.Metadata, "sequence:messageIndex", out var msgIdx))
                    continue;
                if (msgIdx < startIdx || msgIdx > endIdx)
                    continue;
                if (!e.Metadata.TryGetValue("sequence:messageY", out var yObj))
                    continue;

                double y = Convert.ToDouble(yObj, System.Globalization.CultureInfo.InvariantCulture);
                bool isSelf = e.Metadata.ContainsKey("sequence:selfMessage");
                double rowH = isSelf ? messageRowHeight * 2 : messageRowHeight;

                if (y < bandTop)    bandTop    = y;
                // Use y + rowH - halfRow so consecutive bands meet at a shared
                // midpoint rather than overlapping.
                double rowBottom = y + rowH - halfRow;
                if (rowBottom > bandBottom) bandBottom = rowBottom;
            }

            if (bandTop == double.MaxValue)
                continue; // no enclosed messages found

            group.X      = 0;
            group.Y      = bandTop    - halfRow;
            // Width is set to canvasWidth; a second pass below widens both the canvas and the
            // group Width together if a CF tab overflows.
            group.Width  = canvasWidth;
            group.Height = bandBottom - bandTop + halfRow;

            // For CF blocks: make sure the tab label fits within the canvas.
            if (isCf && group.Metadata.TryGetValue("sequence:cfKind", out var cfKindObj)
                && cfKindObj is string cfKind)
            {
                string tabText = string.IsNullOrEmpty(group.Label.Text)
                    ? cfKind
                    : cfKind + " [" + group.Label.Text + "]";
                double tabWidth = EstimateTextWidth(tabText, cfTabFontSize) + 16;
                if (tabWidth + pad > canvasWidth)
                    canvasWidth = tabWidth + pad;
            }
        }

        // Back-fill any CF/rect group widths that were set to a narrower canvasWidth
        // before a long tab label expanded it.
        foreach (var group in diagram.Groups)
        {
            bool isRect = group.Metadata.TryGetValue("sequence:rectGroup", out var rObj) && rObj is true;
            bool isCf   = group.Metadata.TryGetValue("sequence:cfGroup",   out var cObj) && cObj is true;
            if (isRect || isCf)
                group.Width = canvasWidth;
        }

        diagram.Metadata["sequence:canvasWidth"] = canvasWidth;

        // Position activation-bar groups now that message Y coordinates are final.
        LayoutSequenceActivationBars(diagram, orderedEdges, messageRowHeight);
    }

    /// <summary>
    /// Computes the position (X, Y, Width, Height) of each activation bar group.
    /// Activation bars are narrow vertical rectangles drawn on top of a participant's
    /// lifeline, spanning the message range during which the participant is active.
    /// </summary>
    private static void LayoutSequenceActivationBars(
        Diagram diagram,
        List<Edge> orderedEdges,
        double messageRowHeight)
    {
        foreach (var group in diagram.Groups)
        {
            if (!group.Metadata.TryGetValue("sequence:activationGroup", out var isActObj) || isActObj is not true)
                continue;

            if (!group.Metadata.TryGetValue("sequence:activationParticipant", out var pObj)
                || pObj is not string participantId)
                continue;

            if (!diagram.Nodes.TryGetValue(participantId, out var participantNode))
                continue;

            if (!TryGetMetadataInt(group.Metadata, "sequence:activationStartIndex", out var startIdx)
                || !TryGetMetadataInt(group.Metadata, "sequence:activationEndIndex",   out var endIdx))
                continue;

            double halfRow    = messageRowHeight / 2;
            double bandTop    = double.MaxValue;
            double bandBottom = double.MinValue;

            foreach (var e in orderedEdges)
            {
                if (!TryGetMetadataInt(e.Metadata, "sequence:messageIndex", out var msgIdx))
                    continue;
                if (msgIdx < startIdx || msgIdx > endIdx)
                    continue;
                if (!e.Metadata.TryGetValue("sequence:messageY", out var yObj))
                    continue;

                double y = Convert.ToDouble(yObj, System.Globalization.CultureInfo.InvariantCulture);
                bool isSelf = e.Metadata.ContainsKey("sequence:selfMessage");
                double rowH = isSelf ? messageRowHeight * 2 : messageRowHeight;

                if (y < bandTop)    bandTop    = y;
                double rowBottom = y + rowH - halfRow;
                if (rowBottom > bandBottom) bandBottom = rowBottom;
            }

            if (bandTop == double.MaxValue)
                continue; // no enclosed messages found

            // Determine the nesting level so overlapping bars are offset to the right.
            int level = TryGetMetadataInt(group.Metadata, "sequence:activationLevel", out var lvl) ? lvl : 0;

            double lifelineX = participantNode.X + participantNode.Width / 2;
            double barX = lifelineX - ActivationBarWidth / 2 + level * ActivationBarLevelOffset;

            group.X      = barX;
            group.Y      = bandTop    - halfRow;
            group.Width  = ActivationBarWidth;
            group.Height = bandBottom - bandTop + halfRow;
        }
    }

    /// <summary>
    /// Returns the computed box height for a note with the given text using note font metrics.
    /// Shared by both the Y-assignment pre-pass and the <see cref="LayoutSequenceNotes"/> main pass
    /// to guarantee consistent values.
    /// </summary>
    private static double ComputeNoteBoxHeight(string noteText, Theme theme)
    {
        double noteFontSize = theme.FontSize * 0.85;
        double lineH = noteFontSize * 1.3;
        int lineCount = noteText.Split('\n').Length;
        return Math.Max(NoteMinHeight, lineCount * lineH + 2 * NoteVPad);
    }

    /// <summary>
    /// Computes the position (X, Y, Width, Height) of each sequence note group and
    /// adjusts participant positions / canvas width as needed when notes protrude
    /// outside the diagram bounds.
    /// </summary>
    private static void LayoutSequenceNotes(
        Diagram diagram,
        double messageRowHeight,
        Theme theme,
        ref double canvasWidth)
    {
        double noteFontSize = theme.FontSize * 0.85;

        // --- Pre-pass: compute how much extra space leftOf notes need on the left -------
        double extraLeft = 0;
        foreach (var group in diagram.Groups)
        {
            if (!group.Metadata.TryGetValue("sequence:noteGroup", out var isNoteObj) || isNoteObj is not true)
                continue;
            if (!group.Metadata.TryGetValue("sequence:notePosition", out var posObj) || posObj is not string pos || pos != "leftOf")
                continue;
            if (!group.Metadata.TryGetValue("sequence:noteParticipant", out var p1Obj) || p1Obj is not string p1)
                continue;
            if (!diagram.Nodes.TryGetValue(p1, out var p1Node))
                continue;

            string noteText = group.Label.Text;
            string[] noteLines = noteText.Split('\n');
            double textWidth = noteLines.Max(l => EstimateTextWidth(l, noteFontSize));
            double noteWidth  = Math.Max(NoteMinWidth, textWidth + 2 * NoteHPad);

            double lifelineX = p1Node.X + p1Node.Width / 2;
            double noteX = lifelineX - noteWidth - NoteLifelineGap;
            if (noteX < theme.DiagramPadding)
                extraLeft = Math.Max(extraLeft, theme.DiagramPadding - noteX);
        }

        // Shift all participants right when any leftOf note would be clipped at the left edge.
        if (extraLeft > 0)
        {
            foreach (var node in diagram.Nodes.Values)
                node.X += extraLeft;
            canvasWidth += extraLeft;
        }

        // --- Main pass: compute note box geometry ----------------------------------------
        foreach (var group in diagram.Groups)
        {
            if (!group.Metadata.TryGetValue("sequence:noteGroup", out var isNoteObj) || isNoteObj is not true)
                continue;

            if (!group.Metadata.TryGetValue("sequence:noteY", out var noteYObj))
                continue;

            if (!group.Metadata.TryGetValue("sequence:noteParticipant", out var p1Obj)
                || p1Obj is not string p1)
                continue;

            if (!diagram.Nodes.TryGetValue(p1, out var p1Node))
                continue;

            double noteY = Convert.ToDouble(noteYObj, System.Globalization.CultureInfo.InvariantCulture);
            string notePos = group.Metadata.TryGetValue("sequence:notePosition", out var notePosObj)
                && notePosObj is string ps ? ps : "over";

            string noteText = group.Label.Text;
            string[] noteLines = noteText.Split('\n');
            double textWidth = noteLines.Max(l => EstimateTextWidth(l, noteFontSize));
            double noteWidth  = Math.Max(NoteMinWidth, textWidth + 2 * NoteHPad);
            double noteHeight = ComputeNoteBoxHeight(noteText, theme);

            // Vertically center the note box within its sequence row.
            double rowH = Math.Max(messageRowHeight, noteHeight);
            double vOffset = Math.Max(0, (rowH - noteHeight) / 2);
            double boxY = noteY + vOffset;

            double noteX;
            switch (notePos)
            {
                case "rightOf":
                {
                    double lifelineX = p1Node.X + p1Node.Width / 2;
                    noteX = lifelineX + NoteLifelineGap;
                    // Extend canvas if the note box reaches past the current right boundary.
                    double noteRight = noteX + noteWidth;
                    if (noteRight + theme.DiagramPadding > canvasWidth)
                        canvasWidth = noteRight + theme.DiagramPadding;
                    break;
                }
                case "leftOf":
                {
                    double lifelineX = p1Node.X + p1Node.Width / 2;
                    noteX = lifelineX - noteWidth - NoteLifelineGap;
                    break;
                }
                case "over":
                default:
                {
                    Node? p2Node = null;
                    bool hasTwoParticipants =
                        group.Metadata.TryGetValue("sequence:noteParticipant2", out var p2Obj)
                        && p2Obj is string p2
                        && !string.IsNullOrEmpty(p2)
                        && diagram.Nodes.TryGetValue(p2, out p2Node)
                        && p2Node is not null;

                    if (hasTwoParticipants)
                    {
                        double left  = Math.Min(p1Node.X, p2Node!.X);
                        double right = Math.Max(p1Node.X + p1Node.Width, p2Node.X + p2Node.Width);
                        noteX     = left;
                        noteWidth = Math.Max(noteWidth, right - left);
                    }
                    else
                    {
                        // Single participant: align left edge with the participant box.
                        noteX     = p1Node.X;
                        noteWidth = Math.Max(noteWidth, p1Node.Width);
                    }
                    break;
                }
            }

            group.X      = noteX;
            group.Y      = boxY;
            group.Width  = noteWidth;
            group.Height = noteHeight;
        }
    }
}