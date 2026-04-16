using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
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
        bool hasTitle = !string.IsNullOrWhiteSpace(diagram.Title);
        bool hasSubtitle = !string.IsNullOrWhiteSpace(diagram.Subtitle);
        double headingOffset = 0;
        if (hasTitle || hasSubtitle)
            headingOffset += theme.TitleFontSize + 8;
        if (hasTitle && hasSubtitle)
            headingOffset += theme.FontSize + 4;

        double runX = pad;
        double participantStripHeight = ordered.Max(node => node.Height);
        foreach (var node in ordered)
        {
            node.X = runX;
            node.Y = pad + headingOffset + (participantStripHeight - node.Height);
            runX += node.Width + hGap;
        }

        double firstMessageY = pad + headingOffset + participantStripHeight + vGap / 2;
        double messageRowHeight = vGap;

        foreach (var edge in diagram.Edges)
        {
            int msgIdx = TryGetMetadataInt(edge.Metadata, "sequence:messageIndex", out var messageIndex)
                ? messageIndex
                : 0;
            edge.Metadata["sequence:messageY"] = firstMessageY + msgIdx * messageRowHeight;
        }

        int edgeCount = diagram.Edges.Count;
        double canvasHeight = firstMessageY + Math.Max(0, edgeCount) * messageRowHeight + pad;
        diagram.Metadata["sequence:canvasHeight"] = canvasHeight;
    }
}