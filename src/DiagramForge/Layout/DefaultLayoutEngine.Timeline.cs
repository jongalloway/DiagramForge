using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutTimelineDiagram(
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

        var periodNodes = diagram.Nodes.Values
            .Where(n => MetadataEquals(n, "timeline:kind", "period"))
            .OrderBy(n => GetMetadataInt(n.Metadata, "timeline:periodIndex"))
            .ToList();

        if (periodNodes.Count == 0)
            return;

        var eventNodes = diagram.Nodes.Values
            .Where(n => MetadataEquals(n, "timeline:kind", "event"))
            .ToList();

        double colWidth = periodNodes.Max(n => n.Width);
        if (eventNodes.Count > 0)
            colWidth = Math.Max(colWidth, eventNodes.Max(n => n.Width));

        double titleOffset = ComputeHeadingOffset(diagram, theme);

        double periodY = pad + titleOffset;
        double periodRowHeight = periodNodes.Max(node => node.Height);
        for (int i = 0; i < periodNodes.Count; i++)
        {
            var pn = periodNodes[i];
            pn.X = pad + i * (colWidth + hGap);
            pn.Y = periodY + (periodRowHeight - pn.Height) / 2;
            pn.Width = colWidth;
        }

        var nextEventYByPeriod = periodNodes.ToDictionary(
            node => GetMetadataInt(node.Metadata, "timeline:periodIndex"),
            _ => periodY + periodRowHeight + vGap);

        foreach (var eventNode in eventNodes)
        {
            int pIdx = GetMetadataInt(eventNode.Metadata, "timeline:periodIndex");

            var periodNode = periodNodes.Find(n =>
                GetMetadataInt(n.Metadata, "timeline:periodIndex") == pIdx);

            if (periodNode is null)
                continue;

            eventNode.X = periodNode.X;
            eventNode.Y = nextEventYByPeriod[pIdx];
            nextEventYByPeriod[pIdx] += eventNode.Height + vGap;
            eventNode.Width = colWidth;
        }
    }
}