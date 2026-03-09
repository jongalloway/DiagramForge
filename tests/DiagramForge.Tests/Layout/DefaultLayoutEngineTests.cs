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
    public void Layout_ShortLabels_GetMinNodeWidth()
    {
        // Single-char labels shouldn't shrink below the MinNodeWidth floor —
        // tiny skinny boxes look bad and are hard to click.
        var diagram = new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"));

        _engine.Layout(diagram, _theme);

        double minW = diagram.LayoutHints.MinNodeWidth;
        Assert.All(diagram.Nodes.Values, n => Assert.Equal(minW, n.Width));
    }

    [Fact]
    public void Layout_LongLabel_WidensNodeBeyondMinimum()
    {
        // The driving bug: "Not Important / Not Urgent" overflowing a 120px box.
        var diagram = new Diagram()
            .AddNode(new Node("short", "OK"))
            .AddNode(new Node("long", "Not Important / Not Urgent"));

        _engine.Layout(diagram, _theme);

        double minW = diagram.LayoutHints.MinNodeWidth;
        Assert.Equal(minW, diagram.Nodes["short"].Width);
        Assert.True(diagram.Nodes["long"].Width > minW,
            $"Long label width {diagram.Nodes["long"].Width} should exceed MinNodeWidth {minW}");
    }

    [Fact]
    public void Layout_LongerLabel_YieldsWiderNode_Monotonic()
    {
        // Don't pin exact pixel values (the glyph-advance heuristic may be tuned),
        // but strictly longer text must never produce a narrower box.
        var diagram = new Diagram()
            .AddNode(new Node("a", "Twelve chars"))           // 12
            .AddNode(new Node("b", "Twenty-four characters")); // 24

        _engine.Layout(diagram, _theme);

        Assert.True(diagram.Nodes["b"].Width > diagram.Nodes["a"].Width);
    }

    [Fact]
    public void Layout_VariableWidths_PreservesGapBetweenLayers_Horizontal()
    {
        // Running-offset positioning: a wide node in column 0 should push column 1
        // right by at least its own width. Column 1's left edge must not land
        // inside column 0's widest node.
        var diagram = new Diagram();
        diagram.AddNode(new Node("wide", "This is a deliberately wide label"))
               .AddNode(new Node("next", "Next"))
               .AddEdge(new Edge("wide", "next"));
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;

        _engine.Layout(diagram, _theme);

        var wide = diagram.Nodes["wide"];
        var next = diagram.Nodes["next"];

        // No overlap: next column starts at or beyond wide's right edge.
        Assert.True(next.X >= wide.X + wide.Width,
            $"next.X ({next.X}) should be >= wide.X + wide.Width ({wide.X + wide.Width})");

        // Gap respected within a tolerance (don't over-constrain the exact value).
        double gap = next.X - (wide.X + wide.Width);
        Assert.True(gap >= diagram.LayoutHints.HorizontalSpacing - 1);
    }

    [Fact]
    public void Layout_VariableWidths_ReversalKeepsNodeInsideFrame_RightToLeft()
    {
        // RL mirroring must use each node's own width, not a fixed constant —
        // otherwise a wide node ends up with a negative X after the flip.
        var diagram = new Diagram();
        diagram.AddNode(new Node("wide", "Deliberately wide for reversal test"))
               .AddNode(new Node("thin", "Thin"))
               .AddEdge(new Edge("wide", "thin"));
        diagram.LayoutHints.Direction = LayoutDirection.RightToLeft;

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Nodes.Values, n => Assert.True(n.X >= 0, $"{n.Id}.X = {n.X}"));
        // wide is the source → should end up to the right of thin in RL
        Assert.True(diagram.Nodes["wide"].X > diagram.Nodes["thin"].X);
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

    // ── Groups ────────────────────────────────────────────────────────────────

    [Fact]
    public void Layout_GroupBoundingBox_EnclosesAllMembers()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;

        var group = new Group("G", "Group");
        group.ChildNodeIds.AddRange(["A", "B"]);
        diagram.AddGroup(group);

        _engine.Layout(diagram, _theme);

        // Every member node rect must lie strictly inside the group rect.
        foreach (var n in diagram.Nodes.Values)
        {
            Assert.True(group.X < n.X,                        $"{n.Id}: group.X {group.X} should be < node.X {n.X}");
            Assert.True(group.Y < n.Y,                        $"{n.Id}: group.Y {group.Y} should be < node.Y {n.Y}");
            Assert.True(group.X + group.Width  > n.X + n.Width,  $"{n.Id}: group right edge should exceed node right edge");
            Assert.True(group.Y + group.Height > n.Y + n.Height, $"{n.Id}: group bottom edge should exceed node bottom edge");
        }
    }

    [Fact]
    public void Layout_GroupWithLabel_HasExtraTopPadding()
    {
        // The group box must leave room above its members for the label,
        // so a labeled group's top gap > its side gap.
        var diagram = new Diagram().AddNode(new Node("A"));
        var group = new Group("G", "Has A Label");
        group.ChildNodeIds.Add("A");
        diagram.AddGroup(group);

        _engine.Layout(diagram, _theme);

        var a = diagram.Nodes["A"];
        double topGap  = a.Y - group.Y;
        double leftGap = a.X - group.X;
        Assert.True(topGap > leftGap, $"top gap ({topGap}) should exceed side gap ({leftGap}) for a labeled group");
    }

    [Fact]
    public void Layout_GroupWithNoMembers_StaysZeroSized()
    {
        // A group whose child ids don't resolve should not throw and should not
        // end up with nonsense dimensions from a Min()/Max() over an empty set.
        var diagram = new Diagram().AddNode(new Node("A"));
        var group = new Group("G");
        group.ChildNodeIds.Add("missing");
        diagram.AddGroup(group);

        var ex = Record.Exception(() => _engine.Layout(diagram, _theme));
        Assert.Null(ex);
        Assert.Equal(0, group.Width);
        Assert.Equal(0, group.Height);
    }

    [Fact]
    public void Layout_GroupInFirstColumn_DoesNotGoNegative()
    {
        // Driving case: the group's top/left padding (especially the label-height
        // top inset) can exceed DiagramPadding when a member sits in the very first
        // layer. The engine must shift the whole diagram, not let the rect clip.
        var diagram = new Diagram().AddNode(new Node("A"));
        var group = new Group("G", "Label pushes the top up");
        group.ChildNodeIds.Add("A");
        diagram.AddGroup(group);

        _engine.Layout(diagram, _theme);

        Assert.True(group.X >= 0, $"group.X = {group.X}");
        Assert.True(group.Y >= 0, $"group.Y = {group.Y}");
        Assert.All(diagram.Nodes.Values, n =>
        {
            Assert.True(n.X >= 0, $"{n.Id}.X = {n.X}");
            Assert.True(n.Y >= 0, $"{n.Id}.Y = {n.Y}");
        });
    }

    [Fact]
    public void Layout_GroupBox_ComputedAfterMirror_RightToLeft()
    {
        // Group bbox must be computed *after* the RL flip, not before — otherwise
        // the rect lands where the nodes used to be.
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.RightToLeft;

        var group = new Group("G");
        group.ChildNodeIds.Add("B");
        diagram.AddGroup(group);

        _engine.Layout(diagram, _theme);

        var b = diagram.Nodes["B"];
        Assert.True(group.X <= b.X && group.X + group.Width >= b.X + b.Width,
            $"group [{group.X}, {group.X + group.Width}] should enclose B [{b.X}, {b.X + b.Width}] after RL mirror");
    }
}
