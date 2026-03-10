using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidTimelineParser : IMermaidDiagramParser
{
    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.Timeline;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("timeline");

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });

        int periodIndex = -1;
        int eventIndex = 0;

        // Skip index 0 (the "timeline" header).
        for (int i = 1; i < document.Lines.Length; i++)
        {
            var line = document.Lines[i];

            // Optional title declaration.
            if (line.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                builder.WithTitle(line["title ".Length..].Trim());
                continue;
            }

            // Continuation event line: begins with ':'.
            if (line.StartsWith(":", StringComparison.Ordinal))
            {
                if (periodIndex < 0)
                    continue; // Orphan event before any period — ignore.

                AddEvent(builder, line[1..].Trim(), periodIndex, ref eventIndex);
                continue;
            }

            // Period line (bare text), with an optional inline first event separated by ':'.
            // e.g. "Q1 : Research" → period "Q1" + event "Research"
            // e.g. "Q1" → period "Q1" with no inline event
            if (!string.IsNullOrWhiteSpace(line))
            {
                periodIndex++;
                eventIndex = 0;

                int colonIdx = line.IndexOf(':');
                string periodText;
                string? inlineEventText = null;

                if (colonIdx >= 0)
                {
                    periodText = line[..colonIdx].Trim();
                    var candidate = line[(colonIdx + 1)..].Trim();
                    if (!string.IsNullOrEmpty(candidate))
                        inlineEventText = candidate;
                }
                else
                {
                    periodText = line.Trim();
                }

                var periodId = $"period_{periodIndex}";
                var periodNode = new Node(periodId, periodText) { Shape = Shape.Rectangle };
                periodNode.Metadata["timeline:kind"] = "period";
                periodNode.Metadata["timeline:periodIndex"] = periodIndex;
                builder.AddNode(periodNode);

                if (inlineEventText is not null)
                    AddEvent(builder, inlineEventText, periodIndex, ref eventIndex);
            }
        }

        return builder.Build();
    }

    private static void AddEvent(
        IDiagramSemanticModelBuilder builder,
        string eventText,
        int periodIndex,
        ref int eventIndex)
    {
        if (string.IsNullOrEmpty(eventText))
            return;

        var eventId = $"event_{periodIndex}_{eventIndex}";
        var eventNode = new Node(eventId, eventText) { Shape = Shape.Rectangle };
        eventNode.Metadata["timeline:kind"] = "event";
        eventNode.Metadata["timeline:periodIndex"] = periodIndex;
        eventNode.Metadata["timeline:eventIndex"] = eventIndex;

        builder.AddNode(eventNode);
        builder.AddEdge(new Edge($"period_{periodIndex}", eventId));
        eventIndex++;
    }
}
