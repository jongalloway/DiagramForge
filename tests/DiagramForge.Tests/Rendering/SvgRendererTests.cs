using DiagramForge.Layout;
using DiagramForge.Models;
using DiagramForge.Rendering;

namespace DiagramForge.Tests.Rendering;

public class SvgRendererTests
{
    private readonly SvgRenderer _renderer = new();
    private readonly DefaultLayoutEngine _layout = new();
    private readonly Theme _theme = Theme.Default;

    private Diagram BuildAndLayout(Diagram diagram)
    {
        _layout.Layout(diagram, _theme);
        return diagram;
    }

    // ── Basic structure ───────────────────────────────────────────────────────

    [Fact]
    public void Render_ProducesValidSvgRootElement()
    {
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A")));

        string svg = _renderer.Render(diagram, _theme);

        Assert.StartsWith("<svg ", svg);
        Assert.Contains("</svg>", svg);
    }

    [Fact]
    public void Render_ContainsNodeLabelText()
    {
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A", "Hello World")));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("Hello World", svg);
    }

    [Fact]
    public void Render_ContainsDiagramTitle_WhenSet()
    {
        var diagram = BuildAndLayout(new Diagram { Title = "My Diagram" }
            .AddNode(new Node("A")));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("My Diagram", svg);
    }

    [Fact]
    public void Render_ContainsArrowheadMarker_WhenEdgesPresent()
    {
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(new Edge("A", "B")));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("arrowhead", svg);
        Assert.Contains("<path ", svg);
    }

    [Fact]
    public void Render_ContainsEdgeLabel_WhenSet()
    {
        var diagram = new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(new Edge("A", "B") { Label = new DiagramForge.Models.Label("yes") });
        BuildAndLayout(diagram);

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("yes", svg);
    }

    // ── XSS / escaping ────────────────────────────────────────────────────────

    [Fact]
    public void Render_EscapesSpecialCharactersInLabels()
    {
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "<script>alert('xss')</script>")));

        string svg = _renderer.Render(diagram, _theme);

        Assert.DoesNotContain("<script>", svg);
        Assert.Contains("&lt;script&gt;", svg);
    }

    [Fact]
    public void Render_EscapesAmpersandInLabels()
    {
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "Cats & Dogs")));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("Cats &amp; Dogs", svg);
    }

    // ── Dimensions ────────────────────────────────────────────────────────────

    [Fact]
    public void Render_SvgHasWidthAndHeightAttributes()
    {
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A")));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Matches(@"width=""\d+(\.\d+)?""", svg);
        Assert.Matches(@"height=""\d+(\.\d+)?""", svg);
    }

    // ── Null guards ───────────────────────────────────────────────────────────

    [Fact]
    public void Render_NullDiagram_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _renderer.Render(null!, _theme));
    }

    [Fact]
    public void Render_NullTheme_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _renderer.Render(new Diagram(), null!));
    }
}
