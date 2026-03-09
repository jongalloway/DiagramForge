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

    [Fact]
    public void Render_ConceptualProcess_ReturnsSvg()
    {
        const string text = """
            diagram: process
            steps:
              - Plan
              - Build
              - Ship
            """;

        string svg = _renderer.Render(text);

        Assert.StartsWith("<svg ", svg);
    }

    [Fact]
    public void Render_ConceptualCycle_ReturnsSvg()
    {
        const string text = """
            diagram: cycle
            items:
              - Plan
              - Build
              - Test
            """;

        string svg = _renderer.Render(text);

        Assert.StartsWith("<svg ", svg);
    }

    // ── Theme override ────────────────────────────────────────────────────────

    [Fact]
    public void Render_WithCustomTheme_AppliesBackgroundColor()
    {
        var theme = new Theme { BackgroundColor = "#FF0000" };

        string svg = _renderer.Render("flowchart LR\n  A --> B", theme);

        Assert.Contains("#FF0000", svg);
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
