using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseTabListDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        // Handle optional title: section
        int titleLine = FindSectionLine(lines, "title");
        if (titleLine >= 0)
        {
            var titleTrimmed = lines[titleLine].Trim();
            var titleValue = titleTrimmed["title:".Length..].Trim();
            if (!string.IsNullOrEmpty(titleValue))
                builder.WithTitle(titleValue);
        }

        // Handle optional layout: directive (default: "cards")
        string layout = "cards";
        int layoutLine = FindSectionLine(lines, "layout");
        if (layoutLine >= 0)
        {
            var layoutTrimmed = lines[layoutLine].Trim();
            var layoutValue = layoutTrimmed["layout:".Length..].Trim();
            if (!string.IsNullOrEmpty(layoutValue))
            {
                layout = layoutValue.ToLowerInvariant();
                if (layout is not ("cards" or "stacked" or "flat"))
                    throw new DiagramParseException(
                        $"Unknown tablist layout '{layout}'. Supported layouts: cards, stacked, flat.");
            }
        }

        int categoriesLine = FindSectionLine(lines, "categories");
        if (categoriesLine < 0)
            throw new DiagramParseException("Missing required section 'categories:' in tablist diagram.");

        var categories = ReadCategories(lines, categoriesLine + 1);

        if (categories.Count == 0)
            throw new DiagramParseException("Section 'categories' contains no entries.");

        if (categories.Count < 2 || categories.Count > 6)
            throw new DiagramParseException("Tablist diagram requires between 2 and 6 categories.");

        for (int i = 0; i < categories.Count; i++)
        {
            var (title, items) = categories[i];

            var titleNode = new Node($"tab_{i}", title.Label) { IconRef = title.IconRef };
            titleNode.Metadata["tablist:categoryIndex"] = i;
            titleNode.Metadata["tablist:kind"] = "title";
            titleNode.Metadata["tablist:layout"] = layout;
            builder.AddNode(titleNode);

            for (int j = 0; j < items.Count; j++)
            {
                var itemNode = new Node($"tab_{i}_item_{j}", items[j].Label) { IconRef = items[j].IconRef };
                itemNode.Metadata["tablist:categoryIndex"] = i;
                itemNode.Metadata["tablist:itemIndex"] = j;
                itemNode.Metadata["tablist:kind"] = "item";
                itemNode.Metadata["tablist:layout"] = layout;
                builder.AddNode(itemNode);
            }
        }

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });
    }

    /// <summary>
    /// Parses a nested category structure from the lines starting at
    /// <paramref name="startIndex"/>. Each category begins with <c>- title: X</c>
    /// and optionally contains an <c>items:</c> sub-list.
    /// </summary>
    private static List<(IconLabeledText Title, List<IconLabeledText> Items)> ReadCategories(string[] lines, int startIndex)
    {
        var result = new List<(IconLabeledText Title, List<IconLabeledText> Items)>();
        int entryIndent = -1;
        int i = startIndex;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed)) { i++; continue; }

            // Stop at a new zero-indent section header
            if (GetIndent(line) == 0 && !trimmed.StartsWith('-') && trimmed.EndsWith(':'))
                break;

            // Each category starts with "- title: <name>"
            if (!trimmed.StartsWith("- title:", StringComparison.OrdinalIgnoreCase)) { i++; continue; }

            int thisIndent = GetIndent(line);
            if (entryIndent < 0)
                entryIndent = thisIndent;

            if (thisIndent != entryIndent) { i++; continue; }

            var title = ParseIconLabeledText(trimmed["- title:".Length..].Trim());
            var items = new List<IconLabeledText>();
            i++;

            // Parse body of this category (lines indented deeper than the entry marker)
            while (i < lines.Length)
            {
                var bodyLine = lines[i];
                var bodyTrimmed = bodyLine.Trim();

                if (string.IsNullOrEmpty(bodyTrimmed)) { i++; continue; }

                int bodyIndent = GetIndent(bodyLine);

                if (bodyIndent <= entryIndent)
                    break;

                if (bodyTrimmed.Equals("items:", StringComparison.OrdinalIgnoreCase))
                {
                    int itemSectionIndent = bodyIndent;
                    i++;

                    while (i < lines.Length)
                    {
                        var itemLine = lines[i];
                        var itemTrimmed = itemLine.Trim();

                        if (string.IsNullOrEmpty(itemTrimmed)) { i++; continue; }

                        int itemIndent = GetIndent(itemLine);

                        if (itemIndent <= itemSectionIndent)
                            break;

                        if (itemTrimmed.StartsWith('-'))
                            items.Add(ParseIconLabeledText(itemTrimmed[1..].Trim()));

                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }

            result.Add((title, items));
        }

        return result;
    }
}
