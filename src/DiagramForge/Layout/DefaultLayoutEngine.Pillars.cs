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

        // ── Title font is 20% larger and bold ──────────────────────────────────
        double titleFontSize = Math.Round(theme.FontSize * 1.2, 1);

        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;
        double maxSegTextWidth = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("pillars:kind", out var kind) && "segment".Equals(kind as string, StringComparison.Ordinal))
            .Select(n => EnsureIconWidth(n, theme, EstimateTextWidth(n.Label.Text, n.Label.FontSize ?? theme.FontSize) + theme.NodePadding * 2.5))
            .DefaultIfEmpty(0)
            .Max();
        double maxTitleTextWidth = titleNodes.Max(n =>
            EnsureIconWidth(n, theme, EstimateTextWidth(n.Label.Text, titleFontSize) + theme.NodePadding * 2.5));
        double colWidth = Math.Max(minW + 24, Math.Max(maxSegTextWidth, maxTitleTextWidth));

        double titleH = titleNodes.Max(node => EnsureIconHeight(node, Math.Max(nodeH, (titleFontSize * 1.15) + theme.NodePadding * 2.2)));
        double segH = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("pillars:kind", out var kind) && "segment".Equals(kind as string, StringComparison.Ordinal))
            .Select(node => EnsureIconHeight(node, nodeH))
            .DefaultIfEmpty(nodeH)
            .Max();
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

            // ── Title: vibrant fill, contrast text, bolder border ──────────────
            string titleFill = ColorUtils.Blend(ColorUtils.Vibrant(pillarFill, 2.0), pillarFill, 0.5);
            string titleTextColor = ColorUtils.ChooseTextColor(titleFill);

            titleNode.Shape = Shape.RoundedRectangle;
            titleNode.Width = colWidth;
            titleNode.Height = titleH;
            titleNode.X = colX;
            titleNode.Y = curY;
            titleNode.FillColor = titleFill;
            titleNode.StrokeColor = ColorUtils.Darken(titleFill, 0.20);
            titleNode.Metadata["render:strokeWidth"] = theme.StrokeWidth * 2.5;
            titleNode.Label.FontSize = titleFontSize;
            titleNode.Label.FontWeight = "bold";
            titleNode.Label.Color = titleTextColor;
            // Disable the gradient on title nodes so the solid saturated fill shows
            titleNode.Metadata["render:noGradient"] = true;
            SetLabelCenter(titleNode, colWidth / 2, titleH / 2);
            curY += titleH + segGap;

            if (segmentsByPillar.TryGetValue(pillarIdx, out var segments))
            {
                string segFill = ColorUtils.Lighten(pillarFill, 0.25);
                string segStroke = ColorUtils.Lighten(pillarStroke, 0.30);

                foreach (var segNode in segments)
                {
                    segNode.Shape = Shape.RoundedRectangle;
                    segNode.Width = colWidth;
                    segNode.Height = segH;
                    segNode.X = colX;
                    segNode.Y = curY;
                    segNode.FillColor = segFill;
                    segNode.StrokeColor = segStroke;
                    SetLabelCenter(segNode, colWidth / 2, segH / 2);
                    curY += segH + segGap;
                }
            }
        }
    }
}