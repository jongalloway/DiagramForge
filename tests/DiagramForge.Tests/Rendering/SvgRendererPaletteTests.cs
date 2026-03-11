using DiagramForge.Layout;
using DiagramForge.Models;
using DiagramForge.Rendering;

namespace DiagramForge.Tests.Rendering;

public class SvgRendererPaletteTests
{
    private readonly SvgRenderer _renderer = new();
    private readonly DefaultLayoutEngine _layout = new();

    // ── Palette cycling ───────────────────────────────────────────────────────

    [Fact]
    public void Render_WithNodePalette_FirstNodeGetsPaletteColor()
    {
        var theme = new Theme
        {
            NodePalette = ["#AA0000", "#00AA00", "#0000AA"],
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "Alpha")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("#AA0000", svg);
    }

    [Fact]
    public void Render_WithNodePalette_CyclesThroughColors()
    {
        var palette = new List<string> { "#AA0000", "#00AA00", "#0000AA" };
        var theme = new Theme { NodePalette = palette };

        var diagram = new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddNode(new Node("C"))
            .AddNode(new Node("D")); // D wraps back to palette[0]
        _layout.Layout(diagram, theme);

        string svg = _renderer.Render(diagram, theme);

        // All three palette colors should appear
        Assert.Contains("#AA0000", svg);
        Assert.Contains("#00AA00", svg);
        Assert.Contains("#0000AA", svg);
        // First color used twice (for A and D)
        Assert.Equal(2, CountOccurrences(svg, "#AA0000"));
    }

    [Fact]
    public void Render_WithNodePalette_ExplicitFillColorTakesPriority()
    {
        var theme = new Theme
        {
            NodePalette = ["#AA0000", "#00AA00"],
        };
        var nodeWithOverride = new Node("A", "Alpha") { FillColor = "#FFFF00" };
        var diagram = BuildAndLayout(new Diagram().AddNode(nodeWithOverride), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("#FFFF00", svg);
        // Palette color for node 0 should NOT appear (overridden)
        Assert.DoesNotContain("#AA0000", svg);
    }

    [Fact]
    public void Render_WithNodePalette_StrokeDerivedFromPaletteColor()
    {
        // When no NodeStrokePalette is set, the stroke should be a darkened version of the fill.
        var theme = new Theme
        {
            NodePalette = ["#4F81BD"],
        };
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A")), theme);

        string svg = _renderer.Render(diagram, theme);

        // The stroke color (darkened from #4F81BD) should differ from the fill
        string expectedFill   = "#4F81BD";
        string expectedStroke = ColorUtils.Darken("#4F81BD", 0.20);
        Assert.Contains(expectedFill,   svg);
        Assert.Contains(expectedStroke.ToUpperInvariant(), svg.ToUpperInvariant());
    }

    [Fact]
    public void Render_WithNodeStrokePalette_UsesExplicitStrokeColors()
    {
        var theme = new Theme
        {
            NodePalette       = ["#AABBCC"],
            NodeStrokePalette = ["#112233"],
        };
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("#AABBCC", svg);
        Assert.Contains("#112233", svg);
    }

    [Fact]
    public void Render_EmptyNodePalette_FallsBackToThemeFillColor()
    {
        var theme = new Theme
        {
            NodeFillColor = "#DAE8FC",
            NodePalette   = [], // empty list
        };
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("#DAE8FC", svg);
    }

    [Fact]
    public void Render_NullNodePalette_FallsBackToThemeFillColor()
    {
        var theme = new Theme
        {
            NodeFillColor = "#DAE8FC",
            NodePalette   = null,
        };
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("#DAE8FC", svg);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Diagram BuildAndLayout(Diagram diagram, Theme theme)
    {
        _layout.Layout(diagram, theme);
        return diagram;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
