using DiagramForge.Models;

namespace DiagramForge.Tests.Models;

public class DiagramModelTests
{
    [Fact]
    public void Diagram_AddNode_StoresNodeById()
    {
        var diagram = new Diagram();
        var node = new Node("A", "Node A");

        diagram.AddNode(node);

        Assert.True(diagram.Nodes.ContainsKey("A"));
        Assert.Same(node, diagram.Nodes["A"]);
    }

    [Fact]
    public void Diagram_AddNode_ReturnsItself_ForFluentChaining()
    {
        var diagram = new Diagram();
        var result = diagram.AddNode(new Node("A"));

        Assert.Same(diagram, result);
    }

    [Fact]
    public void Diagram_AddEdge_StoresEdge()
    {
        var diagram = new Diagram();
        var edge = new Edge("A", "B");

        diagram.AddEdge(edge);

        Assert.Single(diagram.Edges);
        Assert.Same(edge, diagram.Edges[0]);
    }

    [Fact]
    public void Diagram_AddGroup_StoresGroup()
    {
        var diagram = new Diagram();
        var group = new Group("G1", "Group 1");

        diagram.AddGroup(group);

        Assert.Single(diagram.Groups);
        Assert.Same(group, diagram.Groups[0]);
    }

    [Fact]
    public void Diagram_FluentApi_ChainsAllOperations()
    {
        var diagram = new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(new Edge("A", "B"))
            .AddGroup(new Group("G"));

        Assert.Equal(2, diagram.Nodes.Count);
        Assert.Single(diagram.Edges);
        Assert.Single(diagram.Groups);
    }

    [Fact]
    public void Node_DefaultShape_IsRoundedRectangle()
    {
        var node = new Node("X");

        Assert.Equal(Shape.RoundedRectangle, node.Shape);
    }

    [Fact]
    public void Node_LabelText_DefaultsToId_WhenSingleArgCtor()
    {
        var node = new Node("myId");

        Assert.Equal("myId", node.Label.Text);
    }

    [Fact]
    public void Edge_DefaultArrowHead_IsArrow()
    {
        var edge = new Edge("A", "B");

        Assert.Equal(ArrowHeadStyle.Arrow, edge.ArrowHead);
    }

    [Fact]
    public void Edge_DefaultLineStyle_IsSolid()
    {
        var edge = new Edge("A", "B");

        Assert.Equal(EdgeLineStyle.Solid, edge.LineStyle);
    }

    [Fact]
    public void LayoutHints_Defaults_AreReasonable()
    {
        var hints = new LayoutHints();

        Assert.Equal(LayoutDirection.TopToBottom, hints.Direction);
        Assert.True(hints.HorizontalSpacing > 0);
        Assert.True(hints.VerticalSpacing > 0);
        Assert.True(hints.MinNodeWidth > 0);
        Assert.True(hints.MaxNodeWidth > hints.MinNodeWidth);
        Assert.True(hints.MinNodeHeight > 0);
    }

    [Fact]
    public void Theme_Default_HasNonEmptyValues()
    {
        var theme = Theme.Default;

        Assert.False(string.IsNullOrEmpty(theme.BackgroundColor));
        Assert.False(string.IsNullOrEmpty(theme.FontFamily));
        Assert.True(theme.FontSize > 0);
        Assert.True(theme.BorderRadius >= 0);
    }
}
