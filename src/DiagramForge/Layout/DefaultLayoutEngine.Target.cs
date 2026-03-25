using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutTargetDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        if (!diagram.Nodes.TryGetValue("center", out var centerNode))
            return;

        var ringNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("target:kind", out var kind) && "ring".Equals(kind as string, StringComparison.Ordinal))
            .OrderBy(n => GetMetadataInt(n.Metadata, "target:ringIndex"))
            .ToList();

        var cardNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("target:kind", out var kind) && "card".Equals(kind as string, StringComparison.Ordinal))
            .OrderBy(n => GetMetadataInt(n.Metadata, "target:ringIndex"))
            .ToList();

        if (ringNodes.Count == 0 || cardNodes.Count != ringNodes.Count)
            return;

        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;
        bool isLightBackground = ColorUtils.IsLight(theme.BackgroundColor);

        double centerFontSize = Math.Round(theme.FontSize * 1.08, 1);
        centerNode.Label.FontSize = centerFontSize;
        PrepareLabelLines(centerNode.Label, theme, diagram.LayoutHints);

        double centerTextWidth = EstimateTextWidth(centerNode.Label, centerFontSize);
        double centerDiameter = Math.Max(minW * 1.45, EnsureIconWidth(centerNode, theme, centerTextWidth + theme.NodePadding * 3.4));
        centerDiameter = EnsureIconHeight(centerNode, Math.Max(centerDiameter, 118));

        int ringCount = ringNodes.Count;
        double ringStrokeWidth = Math.Max(26, 39 - Math.Max(0, ringCount - 3) * 2.5);
        double ringGap = 10;
        double innerRingRadius = (centerDiameter / 2) + 18 + (ringStrokeWidth / 2);

        double[] radii = new double[ringCount];
        for (int i = 0; i < ringCount; i++)
        {
            int distanceFromCenter = ringCount - 1 - i;
            radii[i] = innerRingRadius + distanceFromCenter * (ringStrokeWidth + ringGap);
        }

        double outerExtent = radii[0] + (ringStrokeWidth / 2);
        double outerDiameter = outerExtent * 2;

        double titleFontSize = Math.Round(theme.FontSize * 1.12, 1);
        double descFontSize = Math.Round(theme.FontSize * 0.94, 1);
        double accentWidth = 14;
        double cardGap = 22;
        double descWrapWidth = Math.Max(220, minW * 2.4);

        double totalCardHeight = 0;

        foreach (var cardNode in cardNodes)
        {
            string? description = cardNode.Metadata.TryGetValue("target:description", out var descObj) ? descObj as string : null;
            string[] descLines = string.IsNullOrWhiteSpace(description)
                ? []
                : WrapLabelText(description, descFontSize, descWrapWidth);
            if (descLines.Length > 0)
                cardNode.Metadata["target:description"] = string.Join('\n', descLines);

            double titleWidth = EstimateTextWidth(cardNode.Label.Text, titleFontSize);
            double descWidth = descLines.Length == 0 ? 0 : descLines.Max(line => EstimateTextWidth(line, descFontSize));
            double cardWidth = Math.Max(minW * 2.05, Math.Max(titleWidth, descWidth) + theme.NodePadding * 3.2 + accentWidth);

            double descLineHeight = descFontSize * 1.45;
            double descBlockHeight = descLines.Length == 0 ? 0 : descFontSize + ((descLines.Length - 1) * descLineHeight);
            double cardHeight = Math.Max(nodeH * 1.45, theme.NodePadding * 2.3 + titleFontSize + (descLines.Length == 0 ? 0 : 10 + descBlockHeight));

            cardNode.Width = cardWidth;
            cardNode.Height = cardHeight;
            cardNode.Shape = Shape.RoundedRectangle;
            cardNode.Metadata["target:titleFontSize"] = titleFontSize;
            cardNode.Metadata["target:descFontSize"] = descFontSize;
            cardNode.Metadata["target:accentWidth"] = accentWidth;
            cardNode.Metadata["render:suppressLabel"] = true;
            cardNode.Metadata["render:noGradient"] = true;
            totalCardHeight += cardHeight;
        }

        totalCardHeight += Math.Max(0, cardNodes.Count - 1) * cardGap;

        double blockHeight = Math.Max(outerDiameter, totalCardHeight);
        double cx = pad + outerExtent;
        double cy = pad + titleOffset + (blockHeight / 2);
        double cardStartY = pad + titleOffset + ((blockHeight - totalCardHeight) / 2);
        double cardX = pad + outerDiameter + Math.Max(72, theme.NodePadding * 4);

        centerNode.Shape = Shape.Circle;
        centerNode.Width = centerDiameter;
        centerNode.Height = centerDiameter;
        centerNode.X = cx - centerDiameter / 2;
        centerNode.Y = cy - centerDiameter / 2;
        centerNode.FillColor = ColorUtils.Blend(theme.PrimaryColor, theme.TextColor, isLightBackground ? 0.46 : 0.24);
        centerNode.StrokeColor = ColorUtils.Blend(centerNode.FillColor!, theme.BackgroundColor, isLightBackground ? 0.08 : 0.18);
        centerNode.Metadata["render:noGradient"] = true;
        centerNode.Label.Color = ColorUtils.ChooseTextColor(centerNode.FillColor!, lightTextHex: "#F8FAFC", darkTextHex: "#0F172A");
        SetLabelCenter(centerNode, centerDiameter / 2, centerDiameter / 2);

        if (centerNode.ResolvedIcon is not null)
        {
            double iconSize = Math.Min(72, Math.Max(48, centerDiameter * 0.28));
            centerNode.Metadata["icon:size"] = iconSize;
            centerNode.Metadata["icon:y"] = Math.Max(theme.NodePadding, centerDiameter * 0.16);
        }

        string outerColor = ColorUtils.Blend(theme.EdgeColor, theme.BackgroundColor, isLightBackground ? 0.32 : 0.18);
        string[] ringColors = ThemePaletteResolver.BuildRingColors(theme, ringCount, centerNode.FillColor!, outerColor, isLightBackground);

        for (int i = 0; i < ringCount; i++)
        {
            var ringNode = ringNodes[i];
            var cardNode = cardNodes[i];
            double radius = radii[i];
            double diameter = 2 * (radius + ringStrokeWidth / 2);

            string ringColor = ringColors[i];

            ringNode.Shape = Shape.Circle;
            ringNode.Width = diameter;
            ringNode.Height = diameter;
            ringNode.X = cx - diameter / 2;
            ringNode.Y = cy - diameter / 2;
            ringNode.FillColor = ringColor;
            ringNode.StrokeColor = ringColor;
            ringNode.Metadata["target:ringStrokeWidth"] = ringStrokeWidth;
            ringNode.Metadata["render:suppressLabel"] = true;
            ringNode.Metadata["render:disableFillGradient"] = true;

            cardNode.X = cardX;
            cardNode.Y = cardStartY;
            cardNode.FillColor = ColorUtils.Blend(theme.BackgroundColor, theme.SurfaceColor, isLightBackground ? 0.84 : 0.72);
            cardNode.StrokeColor = ColorUtils.Blend(ringColor, theme.BackgroundColor, isLightBackground ? 0.48 : 0.22);
            cardNode.Label.Color = theme.TextColor;
            cardNode.Metadata["target:accentColor"] = ringColor;

            if (diagram.Edges.FirstOrDefault(e => e.SourceId == ringNode.Id && e.TargetId == cardNode.Id) is Edge connector)
            {
                double cardCenterY = cardNode.Y + (cardNode.Height / 2);
                double angle = Math.Atan2(cardCenterY - cy, cardNode.X - cx);
                double sourceAnchorX = cx + (Math.Cos(angle) * radius);
                double sourceAnchorY = cy + (Math.Sin(angle) * radius);

                connector.Color = ColorUtils.IsLight(theme.BackgroundColor)
                    ? ColorUtils.Blend(theme.TextColor, theme.BackgroundColor, 0.18)
                    : ColorUtils.Blend("#FFFFFF", theme.BackgroundColor, 0.12);
                connector.ArrowHead = ArrowHeadStyle.None;
                connector.Routing = EdgeRouting.Bezier;
                connector.Metadata["target:outlinedConnector"] = true;
                connector.Metadata["target:subtleBezier"] = true;
                connector.Metadata["render:overlay"] = true;
                connector.Metadata["render:sourceAnchorX"] = sourceAnchorX;
                connector.Metadata["render:sourceAnchorY"] = sourceAnchorY;
                connector.Metadata["render:targetAnchorX"] = cardNode.X;
                connector.Metadata["render:targetAnchorY"] = cardCenterY;
                connector.Metadata["target:underlayStartLength"] = Math.Max(10d, ringStrokeWidth * 0.52);
                connector.Metadata["target:underlayEndLength"] = Math.Max(7d, theme.NodePadding * 0.45);
            }

            cardStartY += cardNode.Height + cardGap;
        }

        diagram.Metadata["diagram:titleFontSize"] = theme.TitleFontSize;
    }
}
