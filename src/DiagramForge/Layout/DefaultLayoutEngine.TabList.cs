using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutTabListDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        var titleNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("tablist:kind", out var kind) && "title".Equals(kind as string, StringComparison.Ordinal))
            .OrderBy(n => GetMetadataInt(n.Metadata, "tablist:categoryIndex"))
            .ToList();

        if (titleNodes.Count == 0)
            return;

        // Read layout variant from the first title node
        string layout = titleNodes[0].Metadata.TryGetValue("tablist:layout", out var layoutObj)
            ? layoutObj as string ?? "cards"
            : "cards";

        // Group items by category and hide them (their text merges into the card)
        var itemsByCategory = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("tablist:kind", out var kind) && "item".Equals(kind as string, StringComparison.Ordinal))
            .GroupBy(n => GetMetadataInt(n.Metadata, "tablist:categoryIndex"))
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(n => GetMetadataInt(n.Metadata, "tablist:itemIndex")).ToList());

        // Merge item text into title node metadata and hide item nodes
        foreach (var titleNode in titleNodes)
        {
            int catIdx = GetMetadataInt(titleNode.Metadata, "tablist:categoryIndex");
            if (itemsByCategory.TryGetValue(catIdx, out var items) && items.Count > 0)
            {
                titleNode.Metadata["tablist:description"] = string.Join("\n", items.Select(n => n.Label.Text));
                foreach (var item in items)
                {
                    item.Width = 0;
                    item.Height = 0;
                    item.Metadata["render:hidden"] = true;
                }
            }
        }

        if (layout == "stacked")
            LayoutTabListStacked(diagram, theme, minW, nodeH, pad, titleNodes, itemsByCategory);
        else if (layout == "flat")
            LayoutTabListFlat(diagram, theme, minW, nodeH, pad, titleNodes, itemsByCategory);
        else
            LayoutTabListCards(diagram, theme, minW, nodeH, pad, titleNodes, itemsByCategory);
    }

    /// <summary>
    /// Cards layout – inspired by "4 Steps Callout" / modern infographic cards.
    /// Each row is a rounded card with a bold colored accent block on the left
    /// (containing the category title + optional icon) and a light content area
    /// on the right showing bulleted item descriptions. Clear gaps between cards.
    /// </summary>
    private static void LayoutTabListCards(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad,
        List<Node> titleNodes,
        Dictionary<int, List<Node>> itemsByCategory)
    {
        double titleFontSize = Math.Round(theme.FontSize * 1.35, 1);
        double descFontSize = Math.Round(theme.FontSize * 1.15, 1);
        double descLineHeight = descFontSize * 1.5;
        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;

        // Accent block width: sized for title text
        double accentWidth = Math.Max(minW * 1.0,
            titleNodes.Max(n =>
                EstimateTextWidth(n.Label.Text, titleFontSize) + theme.NodePadding * 3));

        // Content width: based on widest description line (with bullet prefix)
        double maxDescWidth = 0;
        foreach (var titleNode in titleNodes)
        {
            int catIdx = GetMetadataInt(titleNode.Metadata, "tablist:categoryIndex");
            if (itemsByCategory.TryGetValue(catIdx, out var items))
            {
                foreach (var item in items)
                {
                    double w = EstimateTextWidth("\u2022  " + item.Label.Text, descFontSize);
                    if (w > maxDescWidth) maxDescWidth = w;
                }
            }
        }
        double contentWidth = Math.Max(minW * 3.0, maxDescWidth + theme.NodePadding * 5);

        // Reserve space for right-edge icon if any row has one
        bool hasAnyIcon = titleNodes.Any(n => n.IconRef is not null);
        double iconSize = hasAnyIcon ? accentWidth * 0.55 : 0;
        double iconGutter = hasAnyIcon ? accentWidth : 0;
        double cardWidth = accentWidth + contentWidth + iconGutter;

        // Row height per card: driven by whichever is taller – accent or bullet list
        int maxItemCount = itemsByCategory.Values.Select(items => items.Count).DefaultIfEmpty(0).Max();
        double minCardHeight = Math.Max(nodeH * 1.5, titleFontSize * 1.15 + theme.NodePadding * 3);
        double cardHeight = Math.Max(minCardHeight,
            theme.NodePadding * 2.5 + maxItemCount * descLineHeight + theme.NodePadding);
        double rowGap = Math.Max(theme.NodePadding * 1.6, 16);

        IReadOnlyList<string> effectivePalette;
        bool useFallbackPalette;
        if (theme.NodePalette is { Count: > 0 })
        {
            effectivePalette = ThemePaletteResolver.ResolveEffectivePalette(theme);
            useFallbackPalette = !ReferenceEquals(effectivePalette, theme.NodePalette);
        }
        else
        {
            effectivePalette = [theme.NodeFillColor];
            useFallbackPalette = false;
        }

        for (int i = 0; i < titleNodes.Count; i++)
        {
            var titleNode = titleNodes[i];
            double rowY = pad + titleOffset + i * (cardHeight + rowGap);
            string catFill = effectivePalette[i % effectivePalette.Count];

            // Accent block: vibrant saturated color
            string accentFill = ColorUtils.Vibrant(catFill, 2.5);
            string accentTextColor = ColorUtils.ChooseTextColor(accentFill);

            // Content area: very light tint with subtle border
            string contentFill = ColorUtils.Lighten(catFill, 0.88);
            string contentStroke = ColorUtils.Lighten(catFill, 0.55);

            titleNode.Width = cardWidth;
            titleNode.Height = cardHeight;
            titleNode.X = pad;
            titleNode.Y = rowY;
            titleNode.FillColor = accentFill;
            titleNode.StrokeColor = ColorUtils.Lighten(accentFill, 0.25);
            titleNode.Label.FontSize = titleFontSize;
            titleNode.Label.FontWeight = "bold";
            titleNode.Label.Color = accentTextColor;
            if (!useFallbackPalette)
                titleNode.Metadata["render:noGradient"] = true;
            titleNode.Metadata["tablist:band"] = true;
            titleNode.Metadata["tablist:layout"] = "cards";
            titleNode.Metadata["tablist:accentWidth"] = accentWidth;
            titleNode.Metadata["tablist:contentFill"] = contentFill;
            titleNode.Metadata["tablist:contentStroke"] = contentStroke;
            titleNode.Metadata["tablist:descFontSize"] = descFontSize;

            // Position icon centered in the right gutter area
            if (titleNode.IconRef is not null)
            {
                titleNode.Metadata["icon:size"] = iconSize;
                titleNode.Metadata["icon:x"] = cardWidth - iconGutter / 2 - iconSize / 2;
                titleNode.Metadata["icon:y"] = (cardHeight - iconSize) / 2;
            }

            // Center title text in the accent block
            SetLabelCenter(titleNode, accentWidth / 2, cardHeight / 2);
        }
    }

    /// <summary>
    /// Stacked layout – inspired by "Vertical Tab List PPT" / "Tabbed List" infographics.
    /// Compact horizontal rows with a colored number tab on the left and a saturated
    /// content bar on the right containing the title and description. Rows have minimal
    /// spacing for a tight infographic feel.
    /// </summary>
    private static void LayoutTabListStacked(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad,
        List<Node> titleNodes,
        Dictionary<int, List<Node>> itemsByCategory)
    {
        double numberFontSize = Math.Round(theme.FontSize * 2.1, 1);
        double titleFontSize = Math.Round(theme.FontSize * 1.3, 1);
        double descFontSize = Math.Round(theme.FontSize * 1.08, 1);
        double descLineHeight = descFontSize * 1.4;
        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;

        // Number tab width: compact square-ish block for the ordinal number
        double tabWidth = Math.Max(minW * 0.5, numberFontSize * 2.8);

        // Content bar width: based on widest title or description text
        double maxContentTextWidth = 0;
        foreach (var titleNode in titleNodes)
        {
            double tw = EstimateTextWidth(titleNode.Label.Text, titleFontSize);
            if (tw > maxContentTextWidth) maxContentTextWidth = tw;

            int catIdx = GetMetadataInt(titleNode.Metadata, "tablist:categoryIndex");
            if (itemsByCategory.TryGetValue(catIdx, out var items))
            {
                foreach (var item in items)
                {
                    double w = EstimateTextWidth(item.Label.Text, descFontSize);
                    if (w > maxContentTextWidth) maxContentTextWidth = w;
                }
            }
        }
        double contentWidth = Math.Max(minW * 3.5, maxContentTextWidth + theme.NodePadding * 5);

        // Reserve space for right-edge icon if any row has one
        bool hasAnyIcon = titleNodes.Any(n => n.IconRef is not null);
        double iconSize = hasAnyIcon ? tabWidth * 0.55 : 0;
        double iconGutter = hasAnyIcon ? tabWidth : 0;
        double bandWidth = tabWidth + contentWidth + iconGutter;

        // Row height: title line + description lines
        int maxItemCount = itemsByCategory.Values.Select(items => items.Count).DefaultIfEmpty(0).Max();
        double minBandHeight = Math.Max(nodeH * 1.2, numberFontSize * 1.5 + theme.NodePadding * 1.5);
        double bandHeight = Math.Max(minBandHeight,
            theme.NodePadding * 2 + titleFontSize * 1.3 + maxItemCount * descLineHeight + theme.NodePadding);
        double rowGap = 6; // Narrow gap for stacked infographic feel

        IReadOnlyList<string> effectivePalette;
        if (theme.NodePalette is { Count: > 0 })
            effectivePalette = ThemePaletteResolver.ResolveEffectivePalette(theme);
        else
            effectivePalette = [theme.NodeFillColor];

        for (int i = 0; i < titleNodes.Count; i++)
        {
            var titleNode = titleNodes[i];
            double rowY = pad + titleOffset + i * (bandHeight + rowGap);
            string catFill = effectivePalette[i % effectivePalette.Count];

            // Number tab: vivid saturated color
            string tabFill = ColorUtils.Vibrant(catFill, 2.5);
            string tabTextColor = ColorUtils.ChooseTextColor(tabFill);

            // Content bar: the same palette color at medium saturation
            string barFill = ColorUtils.Blend(ColorUtils.Vibrant(catFill, 1.8), catFill, 0.35);
            string barTextColor = ColorUtils.ChooseTextColor(barFill);

            // Subtle border so rows stay visible against similar-toned backgrounds
            string barStroke = ColorUtils.Lighten(barFill, 0.25);
            string tabStroke = ColorUtils.Lighten(tabFill, 0.25);

            titleNode.Width = bandWidth;
            titleNode.Height = bandHeight;
            titleNode.X = pad;
            titleNode.Y = rowY;
            titleNode.FillColor = tabFill;
            titleNode.StrokeColor = "none";
            titleNode.Label.FontSize = numberFontSize;
            titleNode.Label.FontWeight = "bold";
            titleNode.Label.Color = tabTextColor;
            // Stacked renderer uses tablist:tabStroke/contentStroke directly; node-level
            // gradient stroke is not consumed, so always suppress gradient generation.
            titleNode.Metadata["render:noGradient"] = true;
            titleNode.Metadata["tablist:band"] = true;
            titleNode.Metadata["tablist:layout"] = "stacked";
            titleNode.Metadata["tablist:accentWidth"] = tabWidth;
            titleNode.Metadata["tablist:contentFill"] = barFill;
            titleNode.Metadata["tablist:contentStroke"] = barStroke;
            titleNode.Metadata["tablist:tabStroke"] = tabStroke;
            titleNode.Metadata["tablist:barTextColor"] = barTextColor;
            titleNode.Metadata["tablist:descFontSize"] = descFontSize;
            titleNode.Metadata["tablist:titleFontSize"] = titleFontSize;
            titleNode.Metadata["tablist:categoryNumber"] = (i + 1).ToString("D2");
            // Suppress default label rendering – we render number + title ourselves
            titleNode.Metadata["render:suppressLabel"] = true;

            // Position icon centered in the right gutter area
            if (titleNode.IconRef is not null)
            {
                titleNode.Metadata["icon:size"] = iconSize;
                titleNode.Metadata["icon:x"] = bandWidth - iconGutter / 2 - iconSize / 2;
                titleNode.Metadata["icon:y"] = (bandHeight - iconSize) / 2;
            }

            // Center number in the tab area
            SetLabelCenter(titleNode, tabWidth / 2, bandHeight / 2);
        }
    }

    /// <summary>
    /// Flat layout – inspired by "Slide4" (vertical accent line with colored title bars).
    /// A thin vertical accent line runs down the left edge. Each category has a wide
    /// rounded colored title bar followed by plain bulleted items beneath it, producing
    /// a clean, modern look with lots of whitespace.
    /// </summary>
    private static void LayoutTabListFlat(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad,
        List<Node> titleNodes,
        Dictionary<int, List<Node>> itemsByCategory)
    {
        double titleFontSize = Math.Round(theme.FontSize * 1.35, 1);
        double descFontSize = Math.Round(theme.FontSize * 1.15, 1);
        double descLineHeight = descFontSize * 1.6;
        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;

        // Accent line: narrow vertical stripe on the left
        double accentLineWidth = 5;
        double accentLineGap = 12;

        // Title bar width: full width minus accent line area
        double maxTitleTextWidth = titleNodes.Max(n =>
            EstimateTextWidth(n.Label.Text, titleFontSize) + theme.NodePadding * 3);
        double maxDescTextWidth = 0;
        foreach (var titleNode in titleNodes)
        {
            int catIdx = GetMetadataInt(titleNode.Metadata, "tablist:categoryIndex");
            if (itemsByCategory.TryGetValue(catIdx, out var items))
            {
                foreach (var item in items)
                {
                    double w = EstimateTextWidth("\u2022  " + item.Label.Text, descFontSize);
                    if (w > maxDescTextWidth) maxDescTextWidth = w;
                }
            }
        }
        double barWidth = Math.Max(minW * 3.5, Math.Max(maxTitleTextWidth, maxDescTextWidth) + theme.NodePadding * 4);

        double titleBarHeight = Math.Max(nodeH * 0.8, titleFontSize * 1.15 + theme.NodePadding * 1.8);

        // Reserve space for right-edge icon if any row has one
        bool hasAnyIcon = titleNodes.Any(n => n.IconRef is not null);
        double iconSize = hasAnyIcon ? titleBarHeight * 0.6 : 0;
        double iconGutter = hasAnyIcon ? titleBarHeight : 0;
        barWidth += iconGutter;
        double totalWidth = accentLineWidth + accentLineGap + barWidth;
        double categoryGap = theme.NodePadding * 1.2;

        IReadOnlyList<string> effectivePalette;
        if (theme.NodePalette is { Count: > 0 })
            effectivePalette = ThemePaletteResolver.ResolveEffectivePalette(theme);
        else
            effectivePalette = [theme.NodeFillColor];

        double curY = pad + titleOffset;

        for (int i = 0; i < titleNodes.Count; i++)
        {
            var titleNode = titleNodes[i];
            int catIdx = GetMetadataInt(titleNode.Metadata, "tablist:categoryIndex");
            var items = itemsByCategory.TryGetValue(catIdx, out var list) ? list : [];
            int itemCount = items.Count;

            string catFill = effectivePalette[i % effectivePalette.Count];
            string titleBarFill = ColorUtils.Vibrant(catFill, 2.0);
            string titleBarTextColor = ColorUtils.ChooseTextColor(titleBarFill);

            // Total category height: title bar + items area
            double itemsAreaHeight = itemCount * descLineHeight + theme.NodePadding;
            double categoryHeight = titleBarHeight + itemsAreaHeight;

            titleNode.Width = totalWidth;
            titleNode.Height = categoryHeight;
            titleNode.X = pad;
            titleNode.Y = curY;
            titleNode.FillColor = titleBarFill;
            titleNode.StrokeColor = "none";
            titleNode.Label.FontSize = titleFontSize;
            titleNode.Label.FontWeight = "bold";
            titleNode.Label.Color = titleBarTextColor;
            // Flat renderer uses tablist:accentColor and StrokeColor="none" directly;
            // node-level gradient stroke is not consumed, so always suppress gradient generation.
            titleNode.Metadata["render:noGradient"] = true;
            titleNode.Metadata["tablist:band"] = true;
            titleNode.Metadata["tablist:layout"] = "flat";
            titleNode.Metadata["tablist:accentLineWidth"] = accentLineWidth;
            titleNode.Metadata["tablist:accentLineGap"] = accentLineGap;
            titleNode.Metadata["tablist:accentColor"] = ColorUtils.Vibrant(effectivePalette[0], 2.5);
            titleNode.Metadata["tablist:barWidth"] = barWidth;
            titleNode.Metadata["tablist:titleBarHeight"] = titleBarHeight;
            titleNode.Metadata["tablist:descFontSize"] = descFontSize;
            titleNode.Metadata["render:suppressLabel"] = true;

            // Position icon centered in the right gutter area of the title bar
            if (titleNode.IconRef is not null)
            {
                titleNode.Metadata["icon:size"] = iconSize;
                titleNode.Metadata["icon:x"] = totalWidth - iconGutter / 2 - iconSize / 2;
                titleNode.Metadata["icon:y"] = (titleBarHeight - iconSize) / 2;
            }

            curY += categoryHeight + categoryGap;
        }
    }
}
