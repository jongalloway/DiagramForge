using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutPillarsDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        var titleNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("pillars:kind", out var kind) && "title".Equals(kind as string, StringComparison.Ordinal))
            .OrderBy(n => GetMetadataInt(n.Metadata, "pillars:pillarIndex"))
            .ToList();

        if (titleNodes.Count == 0)
            return;

        var segmentsByPillar = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("pillars:kind", out var kind) && "segment".Equals(kind as string, StringComparison.Ordinal))
            .GroupBy(n => GetMetadataInt(n.Metadata, "pillars:pillarIndex"))
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(n => GetMetadataInt(n.Metadata, "pillars:segmentIndex")).ToList());

        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;
        double maxTextWidth = diagram.Nodes.Values.Max(n =>
            EstimateTextWidth(n.Label.Text, n.Label.FontSize ?? theme.FontSize));
        double colWidth = Math.Max(minW + 24, maxTextWidth + theme.NodePadding * 2.5);

        double titleH = Math.Max(nodeH, (theme.FontSize * 1.15) + theme.NodePadding * 2.2);
        double segH = nodeH;
        double colGap = Math.Max(theme.NodePadding * 2, 20);
        double segGap = 4;

        string[] palette = theme.NodePalette is { Count: > 0 }
            ? [.. theme.NodePalette]
            : [theme.NodeFillColor];

        for (int i = 0; i < titleNodes.Count; i++)
        {
            var titleNode = titleNodes[i];
            int pillarIdx = GetMetadataInt(titleNode.Metadata, "pillars:pillarIndex");

            double colX = pad + i * (colWidth + colGap);
            double curY = pad + titleOffset;

            string pillarFill = palette[i % palette.Length];
            string pillarStroke = theme.NodeStrokePalette is { Count: > 0 }
                ? theme.NodeStrokePalette[i % theme.NodeStrokePalette.Count]
                : ColorUtils.Darken(pillarFill, 0.18);

            titleNode.Shape = Shape.RoundedRectangle;
            titleNode.Width = colWidth;
            titleNode.Height = titleH;
            titleNode.X = colX;
            titleNode.Y = curY;
            titleNode.FillColor = pillarFill;
            titleNode.StrokeColor = pillarStroke;
            SetLabelCenter(titleNode, colWidth / 2, titleH / 2);
            curY += titleH + segGap;

            if (segmentsByPillar.TryGetValue(pillarIdx, out var segments))
            {
                string segFill = ColorUtils.Lighten(pillarFill, 0.25);

                foreach (var segNode in segments)
                {
                    segNode.Shape = Shape.RoundedRectangle;
                    segNode.Width = colWidth;
                    segNode.Height = segH;
                    segNode.X = colX;
                    segNode.Y = curY;
                    segNode.FillColor = segFill;
                    segNode.StrokeColor = pillarStroke;
                    SetLabelCenter(segNode, colWidth / 2, segH / 2);
                    curY += segH + segGap;
                }
            }
        }
    }
}