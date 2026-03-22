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
        string[] ringColors = BuildTargetRingColors(theme, ringCount, centerNode.FillColor!, outerColor, isLightBackground);

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
            ringNode.Metadata["render:noGradient"] = true;

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

    private static string[] BuildTargetRingColors(Theme theme, int ringCount, string centerColor, string outerColor, bool isLightBackground)
    {
        var colors = new List<string>(ringCount)
        {
            outerColor,
        };

        if (ringCount == 1)
            return [.. colors];

        var candidatePool = new List<string>
        {
            ColorUtils.Vibrant(theme.SecondaryColor, 2.4),
            ColorUtils.Vibrant(theme.AccentColor, 2.4),
            ColorUtils.Vibrant(ColorUtils.Blend(theme.SecondaryColor, theme.AccentColor, 0.5), 2.6),
            ColorUtils.Vibrant(ColorUtils.Blend(theme.AccentColor, theme.PrimaryColor, 0.35), 2.5),
            ColorUtils.Vibrant(ColorUtils.Blend(theme.PrimaryColor, theme.SecondaryColor, 0.25), 2.5),
            RotateHue(theme.SecondaryColor, 34, isLightBackground),
            RotateHue(theme.AccentColor, -34, isLightBackground),
            RotateHue(theme.PrimaryColor, 52, isLightBackground),
        };

        if (theme.NodePalette is { Count: > 0 })
        {
            foreach (var paletteColor in theme.NodePalette)
            {
                candidatePool.Add(ColorUtils.Vibrant(paletteColor, 2.6));
                candidatePool.Add(RotateHue(paletteColor, 28, isLightBackground));
            }
        }

        var distinctCandidates = candidatePool
            .Select(color => ColorUtils.Blend(color, theme.BackgroundColor, isLightBackground ? 0.06 : 0.10))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(color => GetHueDistance(color, outerColor) >= 18)
            .ToList();

        while (colors.Count < ringCount)
        {
            string? nextColor = distinctCandidates
                .OrderByDescending(candidate => GetMinimumHueDistance(candidate, colors.Append(centerColor)))
                .ThenByDescending(candidate => ColorUtils.GetContrastRatio(candidate, theme.BackgroundColor))
                .FirstOrDefault();

            if (nextColor is null)
            {
                nextColor = RotateHue(colors[^1], 55, isLightBackground);
            }
            else
            {
                distinctCandidates.Remove(nextColor);
            }

            colors.Add(nextColor);
        }

        return [.. colors];
    }

    private static double GetMinimumHueDistance(string candidate, IEnumerable<string> existing)
        => existing.Min(existingColor => GetHueDistance(candidate, existingColor));

    private static double GetHueDistance(string leftHex, string rightHex)
    {
        double leftHue = GetHue(leftHex);
        double rightHue = GetHue(rightHex);
        double delta = Math.Abs(leftHue - rightHue);
        return Math.Min(delta, 360 - delta);
    }

    private static double GetHue(string hex)
    {
        var (rRaw, gRaw, bRaw) = ColorUtils.ParseHex(hex);
        double r = rRaw / 255d;
        double g = gRaw / 255d;
        double b = bRaw / 255d;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        if (delta < 0.0001)
            return 0;

        double hue = max switch
        {
            _ when max == r => 60 * (((g - b) / delta) % 6),
            _ when max == g => 60 * (((b - r) / delta) + 2),
            _ => 60 * (((r - g) / delta) + 4),
        };

        return hue < 0 ? hue + 360 : hue;
    }

    private static string RotateHue(string hex, double degrees, bool isLightBackground)
    {
        var (rRaw, gRaw, bRaw) = ColorUtils.ParseHex(hex);
        double r = rRaw / 255d;
        double g = gRaw / 255d;
        double b = bRaw / 255d;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        double lightness = (max + min) / 2;
        double saturation = delta < 0.0001
            ? 0
            : delta / (1 - Math.Abs(2 * lightness - 1));
        double hue = (GetHue(hex) + degrees) % 360;
        if (hue < 0)
            hue += 360;

        saturation = Math.Clamp(Math.Max(saturation, 0.46), 0, 0.88);
        lightness = Math.Clamp(isLightBackground ? Math.Min(lightness, 0.42) : Math.Max(lightness, 0.48), 0.24, 0.62);

        return FromHsl(hue, saturation, lightness);
    }

    private static string FromHsl(double hue, double saturation, double lightness)
    {
        double chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        double segment = hue / 60d;
        double x = chroma * (1 - Math.Abs((segment % 2) - 1));

        (double r1, double g1, double b1) = segment switch
        {
            >= 0 and < 1 => (chroma, x, 0d),
            >= 1 and < 2 => (x, chroma, 0d),
            >= 2 and < 3 => (0d, chroma, x),
            >= 3 and < 4 => (0d, x, chroma),
            >= 4 and < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };

        double match = lightness - (chroma / 2);
        return ColorUtils.ToHex(
            (int)Math.Round((r1 + match) * 255),
            (int)Math.Round((g1 + match) * 255),
            (int)Math.Round((b1 + match) * 255));
    }
}
