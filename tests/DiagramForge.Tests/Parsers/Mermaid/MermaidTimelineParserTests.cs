using DiagramForge.Layout;
using DiagramForge.Models;
using DiagramForge.Parsers.Mermaid;

namespace DiagramForge.Tests.Parsers.Mermaid;

public class MermaidTimelineParserTests
{
    private readonly MermaidParser _parser = new();
    private readonly DefaultLayoutEngine _layout = new();
    private readonly Theme _theme = Theme.Default;

    // ── CanParse ──────────────────────────────────────────────────────────────

    [Fact]
    public void CanParse_ReturnsTrue_ForTimelineHeader()
    {
        Assert.True(_parser.CanParse("timeline\n  Q1 : Research"));
    }

    [Fact]
    public void CanParse_ReturnsFalse_ForNonMermaidInput()
    {
        Assert.False(_parser.CanParse("diagram: process\nsteps:\n  - A"));
    }

    // ── Title ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_TitleLine_SetsDiagramTitle()
    {
        const string input = """
            timeline
                title Product Roadmap
                Q1 : Research
            """;

        var diagram = _parser.Parse(input);

        Assert.Equal("Product Roadmap", diagram.Title);
    }

    [Fact]
    public void Parse_NoTitleLine_DiagramTitleIsNull()
    {
        var diagram = _parser.Parse("timeline\n  Q1 : Research");

        Assert.Null(diagram.Title);
    }

    // ── Periods ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SinglePeriod_ProducesOnePeriodNode()
    {
        var diagram = _parser.Parse("timeline\n  Q1 : Research");

        var periods = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "period")
            .ToList();

        Assert.Single(periods);
        Assert.Equal("Q1", periods[0].Label.Text);
    }

    [Fact]
    public void Parse_PeriodWithInlineEvent_ProducesBothNodes()
    {
        var diagram = _parser.Parse("timeline\n  Q1 : Research");

        var periods = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "period")
            .ToList();
        var events = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "event")
            .ToList();

        Assert.Single(periods);
        Assert.Single(events);
        Assert.Equal("Research", events[0].Label.Text);
    }

    [Fact]
    public void Parse_MultiplePeriods_ProducesCorrectCount()
    {
        const string input = """
            timeline
                Q1 : Research
                Q2 : Beta
                Q3 : GA
            """;

        var diagram = _parser.Parse(input);

        var periods = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "period")
            .ToList();

        Assert.Equal(3, periods.Count);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MultipleEventLinesUnderOnePeriod_AllAttachToThatPeriod()
    {
        const string input = """
            timeline
                Q1 : Research
                   : Prototype
                Q2 : Beta
            """;

        var diagram = _parser.Parse(input);

        // Both events should have periodIndex == 0 (Q1 is period_0).
        var q1Events = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "event"
                     && n.Metadata.TryGetValue("timeline:periodIndex", out var p) && p is 0)
            .ToList();

        Assert.Equal(2, q1Events.Count);
        Assert.Contains(q1Events, e => e.Label.Text == "Research");
        Assert.Contains(q1Events, e => e.Label.Text == "Prototype");
    }

    [Fact]
    public void Parse_EventsUnderDifferentPeriods_HaveCorrectPeriodIndex()
    {
        const string input = """
            timeline
                Q1 : Research
                Q2 : Beta
                   : GA
            """;

        var diagram = _parser.Parse(input);

        var q2Events = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "event"
                     && n.Metadata.TryGetValue("timeline:periodIndex", out var p) && p is 1)
            .ToList();

        Assert.Equal(2, q2Events.Count);
    }

    [Fact]
    public void Parse_EventBeforePeriod_IsIgnored()
    {
        // Orphan event before any period should not produce a node.
        const string input = """
            timeline
                : orphan
                Q1
            """;

        var diagram = _parser.Parse(input);

        var allEvents = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "event")
            .ToList();

        Assert.Empty(allEvents);
    }

    // ── Edges ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EventLine_CreatesEdgeFromPeriodToEvent()
    {
        const string input = """
            timeline
                Q1 : Research
                   : Prototype
            """;

        var diagram = _parser.Parse(input);

        Assert.Equal(2, diagram.Edges.Count);
        Assert.All(diagram.Edges, e => Assert.Equal("period_0", e.SourceId));
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    [Fact]
    public void Layout_PeriodNodes_AreInSingleRowAtEqualY()
    {
        const string input = """
            timeline
                Q1 : Research
                Q2 : Beta
                Q3 : GA
            """;

        var diagram = _parser.Parse(input);
        _layout.Layout(diagram, _theme);

        var periods = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "period")
            .OrderBy(n => Convert.ToInt32(n.Metadata["timeline:periodIndex"], System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        Assert.Equal(3, periods.Count);
        double firstY = periods[0].Y;
        Assert.All(periods, p => Assert.Equal(firstY, p.Y));
    }

    [Fact]
    public void Layout_PeriodNodes_HaveStrictlyIncreasingX()
    {
        const string input = """
            timeline
                Q1 : Research
                Q2 : Beta
                Q3 : GA
            """;

        var diagram = _parser.Parse(input);
        _layout.Layout(diagram, _theme);

        var periods = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "period")
            .OrderBy(n => Convert.ToInt32(n.Metadata["timeline:periodIndex"], System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        for (int i = 1; i < periods.Count; i++)
            Assert.True(periods[i].X > periods[i - 1].X,
                $"period[{i}].X ({periods[i].X}) should be > period[{i - 1}].X ({periods[i - 1].X})");
    }

    [Fact]
    public void Layout_EventNodes_AreBelowTheirPeriod()
    {
        const string input = """
            timeline
                Q1 : Research
                   : Prototype
            """;

        var diagram = _parser.Parse(input);
        _layout.Layout(diagram, _theme);

        var period = diagram.Nodes.Values
            .Single(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "period");

        var events = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "event")
            .ToList();

        Assert.All(events, e =>
            Assert.True(e.Y > period.Y, $"event.Y ({e.Y}) should be below period.Y ({period.Y})"));
    }

    [Fact]
    public void Layout_AllPeriodNodes_HaveEqualWidth()
    {
        const string input = """
            timeline
                Q1 : Research
                Q2 : A longer period label
                Q3 : GA
            """;

        var diagram = _parser.Parse(input);
        _layout.Layout(diagram, _theme);

        var periods = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "period")
            .ToList();

        double firstWidth = periods[0].Width;
        Assert.All(periods, p => Assert.Equal(firstWidth, p.Width));
    }

    [Fact]
    public void Layout_WithTitle_PeriodRowStartsBelowTitle()
    {
        // With a title the period row must be pushed down enough to clear the title
        // text. Without the offset the title (at y≈DiagramPadding-4) visually
        // overlaps the first node row (at y=DiagramPadding).
        const string withTitle = """
            timeline
                title Product Roadmap
                Q1 : Research
            """;

        const string withoutTitle = """
            timeline
                Q1 : Research
            """;

        var diagramWithTitle = _parser.Parse(withTitle);
        var diagramWithoutTitle = _parser.Parse(withoutTitle);

        _layout.Layout(diagramWithTitle, _theme);
        _layout.Layout(diagramWithoutTitle, _theme);

        double periodYWithTitle = diagramWithTitle.Nodes.Values
            .Single(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "period").Y;

        double periodYWithoutTitle = diagramWithoutTitle.Nodes.Values
            .Single(n => n.Metadata.TryGetValue("timeline:kind", out var k) && k is "period").Y;

        // The titled diagram's period row must start strictly below the un-titled one.
        Assert.True(periodYWithTitle > periodYWithoutTitle,
            $"Period row with title ({periodYWithTitle}) should be below period row without title ({periodYWithoutTitle})");

        // And the gap must be at least the title font size + a small margin.
        double gap = periodYWithTitle - periodYWithoutTitle;
        Assert.True(gap >= _theme.TitleFontSize,
            $"Gap ({gap}) should be >= TitleFontSize ({_theme.TitleFontSize}) to avoid title/node overlap");
    }

    // ── DiagramType ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_DiagramType_IsTimeline()
    {
        var diagram = _parser.Parse("timeline\n  Q1 : Research");

        Assert.Equal("timeline", diagram.DiagramType);
    }
}
