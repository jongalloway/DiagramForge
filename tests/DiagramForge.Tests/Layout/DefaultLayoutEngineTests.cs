using DiagramForge.Layout;
using DiagramForge.Models;

namespace DiagramForge.Tests.Layout;

public class DefaultLayoutEngineTests
{
    private readonly DefaultLayoutEngine _engine = new();
    private readonly Theme _theme = Theme.Default;

    [Fact]
    public void Layout_SingleNode_AssignsNonNegativePosition()
    {
        var diagram = new Diagram()
            .AddNode(new Node("A"));

        _engine.Layout(diagram, _theme);

        var node = diagram.Nodes["A"];
        Assert.True(node.X >= 0);
        Assert.True(node.Y >= 0);
        Assert.True(node.Width > 0);
        Assert.True(node.Height > 0);
    }

    [Fact]
    public void Layout_LinearChain_AssignsIncreasingPositions_TopToBottom()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddNode(new Node("C"))
               .AddEdge(new Edge("A", "B"))
               .AddEdge(new Edge("B", "C"));
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;

        _engine.Layout(diagram, _theme);

        double yA = diagram.Nodes["A"].Y;
        double yB = diagram.Nodes["B"].Y;
        double yC = diagram.Nodes["C"].Y;

        Assert.True(yA < yB, $"Expected A.Y ({yA}) < B.Y ({yB})");
        Assert.True(yB < yC, $"Expected B.Y ({yB}) < C.Y ({yC})");
    }

    [Fact]
    public void Layout_LinearChain_AssignsIncreasingPositions_LeftToRight()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;

        _engine.Layout(diagram, _theme);

        Assert.True(diagram.Nodes["A"].X < diagram.Nodes["B"].X);
    }

    [Fact]
    public void Layout_EmptyDiagram_DoesNotThrow()
    {
        var diagram = new Diagram();

        var ex = Record.Exception(() => _engine.Layout(diagram, _theme));
        Assert.Null(ex);
    }

    [Fact]
    public void Layout_AllNodes_HaveSameSizeByDefault()
    {
        var diagram = new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddNode(new Node("C"));

        _engine.Layout(diagram, _theme);

        var widths = diagram.Nodes.Values.Select(n => n.Width).Distinct().ToList();
        var heights = diagram.Nodes.Values.Select(n => n.Height).Distinct().ToList();

        Assert.Single(widths);
        Assert.Single(heights);
    }

    [Fact]
    public void Layout_NullDiagram_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Layout(null!, _theme));
    }

    [Fact]
    public void Layout_NullTheme_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _engine.Layout(new Diagram(), null!));
    }
}
