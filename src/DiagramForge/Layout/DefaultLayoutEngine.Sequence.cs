using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    // Width of the right-side loopback arc rendered for self-messages.
    // This value is stored in sequence:selfMessageLoopWidth edge metadata so the
    // renderer always reads the same value that the canvas-width calculation uses.
    private const double SelfMessageLoopWidth = 40;

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

        double runX = pad;
        double participantStripHeight = ordered.Max(node => node.Height);
        foreach (var node in ordered)
        {
            node.X = runX;
            node.Y = pad + (participantStripHeight - node.Height);
            runX += node.Width + hGap;
        }

        double firstMessageY = pad + participantStripHeight + vGap / 2;
        double messageRowHeight = vGap;

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
        // so the loopback arc is not clipped at the canvas boundary.
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
            double arcRight = selfNode.X + selfNode.Width + SelfMessageLoopWidth;
            if (arcRight > maxNodeRight + extraRight)
                extraRight = arcRight - maxNodeRight;
        }
        diagram.Metadata["sequence:canvasWidth"] = maxNodeRight + extraRight + pad;
    }
}