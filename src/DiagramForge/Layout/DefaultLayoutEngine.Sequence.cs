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
        bool hasAutonumber = diagram.Metadata.ContainsKey("sequence:autonumber");
        double autonumberExtraLeft = hasAutonumber ? SequenceAutonumberBadgeExtraLeft : 0;

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

        // Self-messages (source == target) need 2× the normal row height to
        // accommodate a loopback arc. Walk edges in message-index order so that
        // the running-Y accumulates correctly regardless of storage order.
        var orderedEdges = diagram.Edges
            .OrderBy(e => TryGetMetadataInt(e.Metadata, "sequence:messageIndex", out var idx) ? idx : 0)
            .ToList();

        double runY = firstMessageY;
        foreach (var edge in orderedEdges)
        {
            bool isSelf = string.Equals(edge.SourceId, edge.TargetId, StringComparison.Ordinal);
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
        diagram.Metadata["sequence:canvasWidth"] = maxNodeRight + extraRight + pad;
    }
}