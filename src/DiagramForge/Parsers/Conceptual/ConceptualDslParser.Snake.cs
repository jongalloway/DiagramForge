using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseSnakeDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
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

        int sectionLine = FindSectionLine(lines, "steps");
        if (sectionLine < 0)
            throw new DiagramParseException("Missing required section 'steps:' in snake diagram.");

        var items = ReadListItems(lines, sectionLine + 1);
        if (items.Count == 0)
            throw new DiagramParseException("Section 'steps' contains no items.");

        if (items.Count < 3)
            throw new DiagramParseException(
                $"Snake diagram requires at least 3 steps, but {items.Count} was provided.");

        for (int i = 0; i < items.Count; i++)
        {
            var raw = items[i];

            // First, extract any icon reference so we don't confuse icon colons
            // (e.g. "icon:builtin:cloud") with the label:description separator.
            var spec = ParseIconLabeledText(raw);
            string textAfterIcon = spec.Label;
            string? iconRef = spec.IconRef;

            // Now look for "Label: Description" in the text remaining after icon extraction.
            string stepLabel;
            string? description = null;

            int colonIdx = textAfterIcon.IndexOf(':');
            if (colonIdx >= 0)
            {
                stepLabel = textAfterIcon[..colonIdx].Trim();
                var desc = textAfterIcon[(colonIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(desc))
                    description = desc;
            }
            else
            {
                stepLabel = textAfterIcon;
            }

            var node = new Node($"node_{i}", stepLabel) { IconRef = iconRef };
            node.Metadata["snake:stepIndex"] = i;
            if (description is not null)
                node.Metadata["snake:description"] = description;
            builder.AddNode(node);
        }

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });
    }
}
