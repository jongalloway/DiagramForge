using System.Globalization;
using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Tests;

public class DiagramRendererTests
{
    private readonly DiagramRenderer _renderer = new();

    // ── Mermaid flowchart ─────────────────────────────────────────────────────

    [Fact]
    public void Render_MermaidFlowchart_ReturnsSvg()
    {
        string svg = _renderer.Render("flowchart LR\n  A --> B");

        Assert.StartsWith("<svg ", svg);
        Assert.Contains("</svg>", svg);
    }

    [Fact]
    public void Render_MermaidFlowchart_ContainsNodeLabels()
    {
        string svg = _renderer.Render("flowchart LR\n  A[Start] --> B[End]");

        Assert.Contains("Start", svg);
        Assert.Contains("End", svg);
    }

    // ── Conceptual DSL ────────────────────────────────────────────────────────

    // ── Theme override ────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithCustomTheme_AppliesBackgroundColor()
    {
        var theme = new Theme { BackgroundColor = "#FF0000" };

        string svg = _renderer.Render("flowchart LR\n  A --> B", theme);

        Assert.Contains("#FF0000", svg);
    }

    [Fact]
    public void Render_WithFrontmatterTheme_AppliesNamedTheme()
    {
        string svg = _renderer.Render("---\ntheme: github-dark\n---\nflowchart LR\n  A --> B");

        Assert.Contains("#0D1117", svg);
    }

    [Fact]
    public void Render_WithTransparentBackgroundOverride_OmitsCanvasBackgroundRect()
    {
        var theme = Theme.Dracula;

        string svg = _renderer.Render("flowchart LR\n  A --> B", theme, paletteJson: null, transparentBackgroundOverride: true);

        string borderRadius = theme.BorderRadius.ToString("F2", CultureInfo.InvariantCulture);
        Assert.DoesNotContain($"fill=\"{theme.BackgroundColor}\" rx=\"{borderRadius}\" ry=\"{borderRadius}\"", svg);
    }

    [Fact]
    public void Render_WithFrontmatterTransparentTrue_OmitsCanvasBackgroundRect()
    {
        string svg = _renderer.Render("---\ntransparent: true\n---\nflowchart LR\n  A --> B");

        Assert.DoesNotContain("fill=\"#FCFCFD\" rx=\"8.00\" ry=\"8.00\"", svg);
    }

    [Fact]
    public void Render_WithFrontmatterBorderStyleSolid_DisablesBorderGradients()
    {
        string svg = _renderer.Render("---\ntheme: presentation\nborderStyle: solid\n---\nflowchart LR\n  A --> B");

        Assert.DoesNotContain("node-0-stroke-gradient", svg);
    }

    [Fact]
    public void Render_WithFrontmatterBorderStyleSubtle_UsesTwoStopBorderGradient()
    {
        string svg = _renderer.Render("---\ntheme: presentation\nborderStyle: subtle\n---\nflowchart LR\n  A --> B");

        Assert.Contains("node-0-stroke-gradient", svg);
        Assert.DoesNotContain("offset=\"33%\"", svg);
    }

    [Fact]
    public void Render_WithFrontmatterBorderStyleRainbow_UsesThemeFittedMultiStopGradient()
    {
        string svg = _renderer.Render("---\ntheme: presentation\nborderStyle: rainbow\n---\nflowchart LR\n  A --> B");

        Assert.Contains("offset=\"33%\"", svg);
        Assert.Contains("offset=\"67%\"", svg);
    }

    [Fact]
    public void Render_WithFrontmatterFillStyleFlat_DisablesFillGradients()
    {
        string svg = _renderer.Render("---\ntheme: angled-light\nfillStyle: flat\n---\nflowchart LR\n  A --> B");

        Assert.DoesNotContain("node-0-fill-gradient", svg);
    }

    [Fact]
    public void Render_WithFrontmatterFillStyleSubtle_UsesFillGradients()
    {
        string svg = _renderer.Render("---\ntheme: prism\nfillStyle: subtle\n---\nflowchart LR\n  A --> B");

        Assert.Contains("node-0-fill-gradient", svg);
    }

    [Fact]
    public void Render_WithFrontmatterFillStyleDiagonalStrong_ProducesDifferentFillStops()
    {
        string subtleSvg = _renderer.Render("---\ntheme: default\nfillStyle: subtle\n---\nflowchart LR\n  A --> B");
        string strongSvg = _renderer.Render("---\ntheme: default\nfillStyle: diagonal-strong\n---\nflowchart LR\n  A --> B");

        Assert.Contains("node-0-fill-gradient", strongSvg);
        Assert.NotEqual(subtleSvg, strongSvg);
    }

    [Fact]
    public void Render_WithFrontmatterShadowStyleSoft_AddsGroupShadowFilter()
    {
        string svg = _renderer.Render("---\ntheme: presentation\nshadowStyle: soft\n---\nflowchart LR\n  subgraph backend [Backend Services]\n    A[API] --> B[Worker]\n  end");

        Assert.Contains("soft-shadow", svg);
        Assert.Contains("feGaussianBlur", svg);
        Assert.Contains("feMergeNode", svg);
    }

    [Fact]
    public void Render_WithFrontmatterShadowStyleNone_RemovesThemeDefaultGroupShadow()
    {
        string svg = _renderer.Render("---\ntheme: presentation\nshadowStyle: none\n---\nflowchart LR\n  subgraph backend [Backend Services]\n    A[API] --> B[Worker]\n  end");

        Assert.DoesNotContain("soft-shadow", svg);
        Assert.DoesNotContain("feGaussianBlur", svg);
    }

    [Fact]
    public void Render_WithJsonThemeFillAndShadowStyles_AppliesThemeDrivenStyles()
    {
        string themeJson = """
        {
          "backgroundColor": "#FFFDF8",
          "nodeFillColor": "#EAF2FF",
          "nodeStrokeColor": "#C8D3E2",
          "groupFillColor": "#FFFFFFE6",
          "groupStrokeColor": "#DAE1EA",
          "edgeColor": "#64748B",
          "textColor": "#111827",
          "titleTextColor": "#111827",
          "subtleTextColor": "#64748B",
          "fillStyle": "diagonal-strong",
          "shadowStyle": "soft",
          "nodePalette": ["#EEF4FF", "#ECF8F1", "#FFF3EA", "#F4ECFF"]
        }
        """;

        var theme = Theme.FromJson(themeJson);
        string svg = _renderer.Render("flowchart LR\n  subgraph backend [Backend Services]\n    A[API] --> B[Worker]\n  end", theme);

        Assert.Contains("node-0-fill-gradient", svg);
        Assert.Contains("feGaussianBlur", svg);
        Assert.Contains("feMergeNode", svg);
    }

    [Fact]
    public void Render_XyChart_WithThemeAccent_UsesAccentDrivenSeriesColors()
    {
        string svg = _renderer.Render("xychart-beta\n    title \"Revenue\"\n    x-axis [Q1, Q2, Q3]\n    y-axis 0 --> 100\n    bar [25, 50, 75]\n    line [20, 55, 80]", Theme.Dracula);

        Assert.Contains(Theme.Dracula.AccentColor, svg);
        Assert.DoesNotContain("#4F81BD", svg);
    }

    // ── Parser registry ───────────────────────────────────────────────────────

    [Fact]
    public void RegisteredSyntaxes_ContainsMermaidAndConceptual()
    {
        Assert.Contains("mermaid", _renderer.RegisteredSyntaxes);
        Assert.Contains("conceptual", _renderer.RegisteredSyntaxes);
    }

    [Fact]
    public void RegisterParser_AddsNewSyntax()
    {
        var renderer = new DiagramRenderer();
        renderer.RegisterParser(new FakeParser());

        Assert.Contains("fake", renderer.RegisteredSyntaxes);
    }

    [Fact]
    public void RegisterParser_CustomParserTakesPriorityOverBuiltIn()
    {
        var intercepted = false;
        var renderer = new DiagramRenderer();
        renderer.RegisterParser(new FakeParser(onParse: () => intercepted = true));

        renderer.Render(FakeParser.Trigger);

        Assert.True(intercepted);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public void Render_UnrecognisedText_ThrowsDiagramParseException()
    {
        Assert.Throws<DiagramParseException>(() =>
            _renderer.Render("this is not a diagram"));
    }

    // ── Constructor null guards ───────────────────────────────────────────────

    [Fact]
    public void Constructor_NullParsers_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DiagramRenderer(
            parsers: null!,
            layoutEngine: new DiagramForge.Layout.DefaultLayoutEngine(),
            svgRenderer: new DiagramForge.Rendering.SvgRenderer(),
            defaultTheme: new Theme()));
    }

    [Fact]
    public void Constructor_NullLayoutEngine_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DiagramRenderer(
            parsers: [],
            layoutEngine: null!,
            svgRenderer: new DiagramForge.Rendering.SvgRenderer(),
            defaultTheme: new Theme()));
    }

    [Fact]
    public void Constructor_NullSvgRenderer_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DiagramRenderer(
            parsers: [],
            layoutEngine: new DiagramForge.Layout.DefaultLayoutEngine(),
            svgRenderer: null!,
            defaultTheme: new Theme()));
    }

    [Fact]
    public void Constructor_NullDefaultTheme_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DiagramRenderer(
            parsers: [],
            layoutEngine: new DiagramForge.Layout.DefaultLayoutEngine(),
            svgRenderer: new DiagramForge.Rendering.SvgRenderer(),
            defaultTheme: null!));
    }

    [Fact]
    public void RegisterParser_NullParser_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _renderer.RegisterParser(null!));
    }

    // ── Test double ───────────────────────────────────────────────────────────

    private sealed class FakeParser(Action? onParse = null) : IDiagramParser
    {
        public const string Trigger = "__fake__";
        public string SyntaxId => "fake";
        public bool CanParse(string text) => text.TrimStart().StartsWith(Trigger, StringComparison.Ordinal);

        public Diagram Parse(string text)
        {
            onParse?.Invoke();
            return new Diagram()
                .AddNode(new Node("X"));
        }
    }
}
