using DiagramForge.Layout;
using DiagramForge.Models;
using DiagramForge.Parsers.Mermaid;

namespace DiagramForge.Tests.Parsers.Mermaid;

public class MermaidXyChartParserTests
{
    private readonly MermaidParser _parser = new();
    private readonly DefaultLayoutEngine _layout = new();
    private readonly Theme _theme = Theme.Default;

    // ── CanParse ──────────────────────────────────────────────────────────────

    [Fact]
    public void CanParse_ReturnsTrue_ForXyChartBetaHeader()
    {
        Assert.True(_parser.CanParse("xychart-beta\n    bar [5000, 6000]"));
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
            xychart-beta
                title "Sales Revenue"
                x-axis [jan, feb, mar]
                bar [5000, 6000, 7500]
            """;

        var diagram = _parser.Parse(input);

        Assert.Equal("Sales Revenue", diagram.Title);
    }

    [Fact]
    public void Parse_NoTitleLine_DiagramTitleIsNull()
    {
        const string input = """
            xychart-beta
                x-axis [jan, feb]
                bar [100, 200]
            """;

        var diagram = _parser.Parse(input);

        Assert.Null(diagram.Title);
    }

    // ── X-Axis ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_XAxisCategories_StoresCategories()
    {
        const string input = """
            xychart-beta
                x-axis [jan, feb, mar, apr]
                bar [1, 2, 3, 4]
            """;

        var diagram = _parser.Parse(input);

        var categories = diagram.Metadata["xychart:categories"] as string[];
        Assert.NotNull(categories);
        Assert.Equal(4, categories.Length);
        Assert.Equal("jan", categories[0]);
        Assert.Equal("apr", categories[3]);
    }

    [Fact]
    public void Parse_XAxisCategories_SetsCategoryCount()
    {
        const string input = """
            xychart-beta
                x-axis [a, b, c]
                bar [10, 20, 30]
            """;

        var diagram = _parser.Parse(input);

        Assert.Equal(3, diagram.Metadata["xychart:categoryCount"]);
    }

    // ── Y-Axis ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_YAxisWithLabelAndRange_StoresBoth()
    {
        const string input = """
            xychart-beta
                x-axis [jan, feb]
                y-axis "Revenue (in $)" 4000 --> 11000
                bar [5000, 6000]
            """;

        var diagram = _parser.Parse(input);

        Assert.Equal("Revenue (in $)", diagram.Metadata["xychart:yLabel"]);
        Assert.Equal(4000.0, diagram.Metadata["xychart:yMin"]);
        Assert.Equal(11000.0, diagram.Metadata["xychart:yMax"]);
    }

    [Fact]
    public void Parse_YAxisOmitted_AutocomputesRange()
    {
        const string input = """
            xychart-beta
                x-axis [a, b]
                bar [100, 500]
            """;

        var diagram = _parser.Parse(input);

        Assert.Equal(0.0, diagram.Metadata["xychart:yMin"]);
        var yMax = (double)diagram.Metadata["xychart:yMax"];
        Assert.True(yMax >= 500, $"Expected yMax >= 500, got {yMax}");
    }

    // ── Bar Series ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SingleBarSeries_CreatesBarNodes()
    {
        const string input = """
            xychart-beta
                x-axis [a, b, c]
                bar [10, 20, 30]
            """;

        var diagram = _parser.Parse(input);

        var barNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "bar")
            .ToList();

        Assert.Equal(3, barNodes.Count);
    }

    [Fact]
    public void Parse_BarSeriesValues_AreStoredInMetadata()
    {
        const string input = """
            xychart-beta
                x-axis [a, b]
                bar [100, 200]
            """;

        var diagram = _parser.Parse(input);

        var barNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "bar")
            .OrderBy(n => Convert.ToInt32(n.Metadata["xychart:categoryIndex"], System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        Assert.Equal(100.0, barNodes[0].Metadata["xychart:value"]);
        Assert.Equal(200.0, barNodes[1].Metadata["xychart:value"]);
    }

    [Fact]
    public void Parse_MultipleBarSeries_CreatesNodesForEachSeries()
    {
        const string input = """
            xychart-beta
                x-axis [a, b]
                bar [10, 20]
                bar [30, 40]
            """;

        var diagram = _parser.Parse(input);

        var barNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "bar")
            .ToList();

        Assert.Equal(4, barNodes.Count);
        Assert.Equal(2, (int)diagram.Metadata["xychart:barSeriesCount"]);
    }

    // ── Line Series ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SingleLineSeries_CreatesLinePointNodes()
    {
        const string input = """
            xychart-beta
                x-axis [a, b, c]
                line [10, 20, 30]
            """;

        var diagram = _parser.Parse(input);

        var lineNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "linePoint")
            .ToList();

        Assert.Equal(3, lineNodes.Count);
    }

    [Fact]
    public void Parse_LineSeriesValues_AreStoredInMetadata()
    {
        const string input = """
            xychart-beta
                x-axis [a, b]
                line [100, 200]
            """;

        var diagram = _parser.Parse(input);

        var lineNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "linePoint")
            .OrderBy(n => Convert.ToInt32(n.Metadata["xychart:categoryIndex"], System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        Assert.Equal(100.0, lineNodes[0].Metadata["xychart:value"]);
        Assert.Equal(200.0, lineNodes[1].Metadata["xychart:value"]);
    }

    // ── Mixed Series ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MixedBarAndLine_CreatesAllNodes()
    {
        const string input = """
            xychart-beta
                x-axis [jan, feb, mar]
                bar [5000, 6000, 7500]
                line [5000, 6000, 7000]
            """;

        var diagram = _parser.Parse(input);

        var barNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "bar")
            .ToList();
        var lineNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "linePoint")
            .ToList();

        Assert.Equal(3, barNodes.Count);
        Assert.Equal(3, lineNodes.Count);
    }

    // ── DiagramType ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SetsDiagramType_ToXychart()
    {
        var diagram = _parser.Parse("xychart-beta\n    bar [10, 20]");

        Assert.Equal("xychart", diagram.DiagramType);
        Assert.Equal("mermaid", diagram.SourceSyntax);
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    [Fact]
    public void Layout_BarNodes_HavePositiveWidthAndHeight()
    {
        const string input = """
            xychart-beta
                x-axis [a, b, c]
                bar [100, 200, 300]
            """;

        var diagram = _parser.Parse(input);
        _layout.Layout(diagram, _theme);

        var barNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "bar")
            .ToList();

        Assert.All(barNodes, n =>
        {
            Assert.True(n.Width > 0, $"Bar {n.Id} width should be > 0");
            Assert.True(n.Height > 0, $"Bar {n.Id} height should be > 0");
        });
    }

    [Fact]
    public void Layout_BarNodes_HigherValueProducesTallerBar()
    {
        const string input = """
            xychart-beta
                x-axis [small, large]
                bar [100, 500]
            """;

        var diagram = _parser.Parse(input);
        _layout.Layout(diagram, _theme);

        var bars = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "bar")
            .OrderBy(n => Convert.ToInt32(n.Metadata["xychart:categoryIndex"], System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        Assert.True(bars[1].Height > bars[0].Height,
            $"Larger value bar height ({bars[1].Height}) should exceed smaller ({bars[0].Height})");
    }

    [Fact]
    public void Layout_LinePoints_ArePositionedWithinChartArea()
    {
        const string input = """
            xychart-beta
                x-axis [a, b, c]
                line [10, 20, 30]
            """;

        var diagram = _parser.Parse(input);
        _layout.Layout(diagram, _theme);

        var lineNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "linePoint")
            .ToList();

        Assert.All(lineNodes, n =>
        {
            Assert.True(n.X >= 0, $"Line point {n.Id} X should be >= 0");
            Assert.True(n.Y >= 0, $"Line point {n.Id} Y should be >= 0");
        });
    }

    [Fact]
    public void Layout_StoresCanvasDimensions()
    {
        const string input = """
            xychart-beta
                x-axis [a, b]
                bar [100, 200]
            """;

        var diagram = _parser.Parse(input);
        _layout.Layout(diagram, _theme);

        Assert.True(diagram.Metadata.ContainsKey("xychart:canvasWidth"));
        Assert.True(diagram.Metadata.ContainsKey("xychart:canvasHeight"));
        Assert.True((double)diagram.Metadata["xychart:canvasWidth"] > 0);
        Assert.True((double)diagram.Metadata["xychart:canvasHeight"] > 0);
    }

    // ── Full pipeline ─────────────────────────────────────────────────────────

    [Fact]
    public void FullPipeline_RendersSvgWithBarsAndAxes()
    {
        const string input = """
            xychart-beta
                title "Sales Revenue"
                x-axis [jan, feb, mar, apr, may, jun]
                y-axis "Revenue (in $)" 4000 --> 11000
                bar [5000, 6000, 7500, 8200, 9500, 10500]
                line [5000, 6000, 7000, 8000, 9500, 10500]
            """;

        var renderer = new DiagramRenderer();
        var svg = renderer.Render(input);

        Assert.StartsWith("<svg", svg);
        Assert.Contains("</svg>", svg);

        // Should contain bar rects (from AppendNode for Rectangle shapes)
        Assert.Contains("<rect", svg);

        // Should contain axis lines
        Assert.Contains("<line", svg);

        // Should contain axis labels
        Assert.Contains("jan", svg);

        // Should contain the title
        Assert.Contains("Sales Revenue", svg);

        // Should contain a polyline for the line series
        Assert.Contains("<polyline", svg);
    }
}
