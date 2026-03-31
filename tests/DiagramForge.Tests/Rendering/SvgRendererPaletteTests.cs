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
        string expectedFill = "#4F81BD";
        string expectedStroke = ColorUtils.Darken("#4F81BD", 0.20);
        Assert.Contains(expectedFill, svg);
        Assert.Contains(expectedStroke.ToUpperInvariant(), svg.ToUpperInvariant());
    }

    [Fact]
    public void Render_WithNodeStrokePalette_UsesExplicitStrokeColors()
    {
        var theme = new Theme
        {
            NodePalette = ["#AABBCC"],
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
            NodePalette = [], // empty list
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
            NodePalette = null,
        };
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("#DAE8FC", svg);
    }

    [Fact]
    public void Render_WithGradients_EmitsGradientDefinitionsForNodePaint()
    {
        var theme = new Theme
        {
            NodeFillColor = "#D0E4FF",
            NodeStrokeColor = "#003087",
            UseGradients = true,
            UseBorderGradients = true,
            GradientStrength = 0.18,
        };
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A", "Alpha")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("linearGradient", svg);
        Assert.Contains("url(#node-0-fill-gradient)", svg);
        Assert.Contains("url(#node-0-stroke-gradient)", svg);
    }

    [Fact]
    public void Render_WithRainbowBorderStops_EmitsMultiStopBorderGradient()
    {
        var theme = new Theme
        {
            NodeFillColor = "#FFFFFF",
            NodeStrokeColor = "#6B7280",
            UseGradients = false,
            UseBorderGradients = true,
            BorderGradientStops = ["#2563EB", "#7C3AED", "#DB2777", "#F97316"],
        };
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A", "Alpha")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("#2563EB", svg);
        Assert.Contains("#7C3AED", svg);
        Assert.Contains("#DB2777", svg);
        Assert.Contains("#F97316", svg);
    }

    [Fact]
    public void Render_WithSoftShadowTheme_AppliesShadowToGroupsOnly()
    {
        var theme = new Theme
        {
            GroupFillColor = "#FFFFFFE6",
            GroupStrokeColor = "#DAE1EA",
            ShadowStyle = "soft",
            ShadowColor = "#0F172A",
            ShadowOpacity = 0.10,
            ShadowBlur = 1.10,
            ShadowOffsetY = 1.10,
        };
        var diagram = new Diagram()
            .AddNode(new Node("A", "Alpha"))
            .AddNode(new Node("B", "Beta"))
            .AddEdge(new Edge("A", "B"));
        var group = new Group("backend", "Backend")
        {
            FillColor = "#FFFFFFE6",
            StrokeColor = "#DAE1EA",
        };
        group.ChildNodeIds.Add("A");
        group.ChildNodeIds.Add("B");
        diagram.AddGroup(group);
        _layout.Layout(diagram, theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("feGaussianBlur", svg);
        Assert.Contains("feMergeNode", svg);
        Assert.Contains("group-0-soft-shadow", svg);
        Assert.DoesNotContain("node-0-soft-shadow", svg);
    }

    [Fact]
    public void Render_WithPresentationTheme_AppliesShadowToRegularNodes()
    {
        var theme = Theme.Presentation;
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "Alpha"))
            .AddNode(new Node("B", "Beta"))
            .AddEdge(new Edge("A", "B")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("node-0-soft-shadow", svg);
        Assert.Contains("filter=\"url(#node-0-soft-shadow)\"", svg);
    }

    // ── Advanced filter effects ───────────────────────────────────────────────

    [Fact]
    public void Render_WithNeumorphicTheme_EmitsNeumorphicFilter()
    {
        var theme = Theme.Neumorphic;
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "Alpha"))
            .AddNode(new Node("B", "Beta"))
            .AddEdge(new Edge("A", "B")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("node-0-neumorphic", svg);
        Assert.Contains("filter=\"url(#node-0-neumorphic)\"", svg);
        // Neumorphic requires both dark and light shadow layers.
        Assert.Contains("dark-shadow", svg);
        Assert.Contains("light-shadow", svg);
    }

    [Fact]
    public void Render_WithGlassTheme_EmitsFrostedGlassFilter()
    {
        var theme = Theme.Glass;
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "Alpha"))
            .AddNode(new Node("B", "Beta"))
            .AddEdge(new Edge("A", "B")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("node-0-frosted-glass", svg);
        Assert.Contains("filter=\"url(#node-0-frosted-glass)\"", svg);
        // Frosted glass uses ambient shadow + inner highlight for a floating panel look.
        Assert.Contains("shadow-blur", svg);
        Assert.Contains("inner-edge", svg);
    }

    [Fact]
    public void Render_WithNeonTheme_EmitsGlassGlowFilter()
    {
        var theme = Theme.Neon;
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "Alpha"))
            .AddNode(new Node("B", "Beta"))
            .AddEdge(new Edge("A", "B")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("node-0-glass-glow", svg);
        Assert.Contains("filter=\"url(#node-0-glass-glow)\"", svg);
        Assert.Contains("feBlend", svg);
    }

    [Theory]
    [InlineData("inner-glass", "inner-glass")]
    [InlineData("ambient-shadow", "ambient-shadow")]
    public void Render_WithEffectStyle_EmitsMatchingFilter(string shadowStyle, string expectedFilterSuffix)
    {
        var theme = new Theme
        {
            ShadowStyle = shadowStyle,
            UseNodeShadows = true,
            ShadowBlur = 1.50,
            ShadowOpacity = 0.14,
            ShadowOffsetY = 1.40,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "Alpha"))
            .AddEdge(new Edge("A", "A")), theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains($"node-0-{expectedFilterSuffix}", svg);
        Assert.Contains($"filter=\"url(#node-0-{expectedFilterSuffix})\"", svg);
    }

    [Fact]
    public void Render_WithNeumorphicGroup_EmitsGroupFilter()
    {
        var theme = new Theme
        {
            ShadowStyle = "neumorphic",
            UseNodeShadows = true,
            ShadowBlur = 2.00,
            ShadowOpacity = 0.18,
            ShadowOffsetY = 1.50,
        };
        var diagram = new Diagram()
            .AddNode(new Node("A", "Alpha"))
            .AddNode(new Node("B", "Beta"))
            .AddEdge(new Edge("A", "B"));
        var group = new Group("g1", "Services");
        group.ChildNodeIds.Add("A");
        group.ChildNodeIds.Add("B");
        diagram.AddGroup(group);
        _layout.Layout(diagram, theme);

        string svg = _renderer.Render(diagram, theme);

        Assert.Contains("group-0-neumorphic", svg);
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
