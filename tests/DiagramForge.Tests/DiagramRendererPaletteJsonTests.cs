using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Tests;

public class DiagramRendererPaletteJsonTests
{
    private readonly DiagramRenderer _renderer = new();
    private const string SimpleDiagram = "flowchart LR\n  A --> B";
    private const string ThreeNodeDiagram = "flowchart LR\n  A --> B\n  A --> C";

    // ── Valid palette JSON ────────────────────────────────────────────────────

    [Fact]
    public void Render_WithPaletteJson_AppliesFirstColor_ToFirstNode()
    {
        string svg = _renderer.Render(SimpleDiagram, theme: null, paletteJson: """["#FF0000","#00FF00","#0000FF"]""");

        Assert.Contains("#FF0000", svg);
    }

    [Fact]
    public void Render_WithPaletteJson_AllColorsAppear_ForThreeNodes()
    {
        string svg = _renderer.Render(ThreeNodeDiagram, theme: null, paletteJson: """["#FF0000","#00FF00","#0000FF"]""");

        Assert.Contains("#FF0000", svg);
        Assert.Contains("#00FF00", svg);
        Assert.Contains("#0000FF", svg);
    }

    [Fact]
    public void Render_WithPaletteJson_OverridesThemePalette()
    {
        var theme = new Theme
        {
            NodePalette = ["#AAAAAA", "#BBBBBB"],
        };

        string svg = _renderer.Render(SimpleDiagram, theme, paletteJson: """["#FF0000","#00FF00","#0000FF"]""");

        // JSON palette overrides theme palette
        Assert.Contains("#FF0000", svg);
        Assert.DoesNotContain("#AAAAAA", svg);
    }

    [Fact]
    public void Render_WithNullPaletteJson_UsesThemePalette()
    {
        var theme = new Theme
        {
            NodePalette = ["#AABBCC"],
        };

        string svg = _renderer.Render(SimpleDiagram, theme, paletteJson: null);

        Assert.Contains("#AABBCC", svg);
    }

    [Fact]
    public void Render_WithPaletteJson_DoesNotMutateOriginalTheme()
    {
        var theme = new Theme
        {
            NodePalette = ["#AAAAAA"],
        };
        string paletteJson = """["#FF0000","#00FF00"]""";

        _ = _renderer.Render(SimpleDiagram, theme, paletteJson);

        // Original theme must be unchanged
        Assert.Single(theme.NodePalette!);
        Assert.Equal("#AAAAAA", theme.NodePalette![0]);
    }

    [Fact]
    public void Render_WithPaletteJson_ThreeCharHexColors_AreAccepted()
    {
        // #RGB shorthand
        string svg = _renderer.Render(SimpleDiagram, theme: null, paletteJson: """["#F00","#0F0","#00F"]""");

        Assert.Contains("<svg ", svg);
    }

    // ── Invalid palette JSON ──────────────────────────────────────────────────

    [Fact]
    public void Render_WithInvalidJson_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _renderer.Render(SimpleDiagram, theme: null, paletteJson: "not json"));
    }

    [Fact]
    public void Render_WithEmptyArray_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _renderer.Render(SimpleDiagram, theme: null, paletteJson: "[]"));
    }

    [Fact]
    public void Render_WithNonHexEntry_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _renderer.Render(SimpleDiagram, theme: null, paletteJson: """["#FF0000","notacolor"]"""));
        Assert.Contains("[1]", ex.Message);
    }

    [Fact]
    public void Render_WithMissingHashPrefix_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _renderer.Render(SimpleDiagram, theme: null, paletteJson: """["FF0000"]"""));
    }

    // ── RGBA / RRGGBBAA palette colors ────────────────────────────────────────

    [Fact]
    public void Render_WithFourCharRgbaColors_IsAccepted()
    {
        // #F80C = #FF8800CC (shorthand with alpha)
        string svg = _renderer.Render(SimpleDiagram, theme: null, paletteJson: """["#F80C","#0F0C"]""");
        Assert.Contains("<svg ", svg);
    }

    [Fact]
    public void Render_WithEightCharRrggbbaaColors_IsAccepted()
    {
        // Semi-transparent colors
        string svg = _renderer.Render(SimpleDiagram, theme: null, paletteJson: """["#FF000080","#00FF0080"]""");
        Assert.Contains("<svg ", svg);
    }

    // ── Null palette JSON leaves behavior unchanged ───────────────────────────

    [Fact]
    public void Render_NullPaletteJson_RendersWithoutError()
    {
        string svg = _renderer.Render(SimpleDiagram, theme: null, paletteJson: null);

        Assert.StartsWith("<svg ", svg);
        Assert.Contains("</svg>", svg);
    }
}
