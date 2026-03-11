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
    public void Layout_VennDiagram_UsesOverlappingCirclePlacement()
    {
        var diagram = new Diagram { DiagramType = "venn" };
        diagram.AddNode(new Node("node_0", "Fast") { Shape = Shape.Circle })
               .AddNode(new Node("node_1", "Cheap") { Shape = Shape.Circle })
               .AddNode(new Node("node_2", "Good") { Shape = Shape.Circle });

        _engine.Layout(diagram, _theme);

        var fast = diagram.Nodes["node_0"];
        var cheap = diagram.Nodes["node_1"];
        var good = diagram.Nodes["node_2"];

        Assert.All(diagram.Nodes.Values, node =>
        {
            Assert.Equal(node.Width, node.Height);
            Assert.Equal(Shape.Circle, node.Shape);
        });

        Assert.True(fast.Y < cheap.Y, $"Expected top node Y ({fast.Y}) to be above left node Y ({cheap.Y}).");
        Assert.Equal(cheap.Y, good.Y);
        Assert.True(fast.X > cheap.X && fast.X < good.X,
            $"Expected top node X ({fast.X}) to sit between left ({cheap.X}) and right ({good.X}) nodes.");
        Assert.True(fast.X < cheap.X + cheap.Width,
            $"Expected top and left circles to overlap, but fast.X ({fast.X}) was not inside cheap's right edge ({cheap.X + cheap.Width}).");
        Assert.True(good.X < fast.X + fast.Width,
            $"Expected top and right circles to overlap, but right.X ({good.X}) was not inside top's right edge ({fast.X + fast.Width}).");
        Assert.True(good.X < cheap.X + cheap.Width,
            $"Expected left and right circles to overlap, but right.X ({good.X}) started beyond left's right edge ({cheap.X + cheap.Width}).");

        Assert.Equal(fast.Width * 0.50, Convert.ToDouble(fast.Metadata["label:centerX"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(fast.Height * 0.24, Convert.ToDouble(fast.Metadata["label:centerY"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(cheap.Width * 0.26, Convert.ToDouble(cheap.Metadata["label:centerX"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(good.Width * 0.74, Convert.ToDouble(good.Metadata["label:centerX"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Layout_VennDiagram_PositionsOverlapTextNodes()
    {
        var diagram = new Diagram { DiagramType = "venn" };

        var top = new Node("node_0", "A") { Shape = Shape.Circle };
        var left = new Node("node_1", "B") { Shape = Shape.Circle };
        var right = new Node("node_2", "C") { Shape = Shape.Circle };
        var overlap = new Node("overlap_abc", "a+b+c");
        overlap.Metadata["venn:kind"] = "overlap";
        overlap.Metadata["venn:region"] = "abc";
        overlap.Metadata["render:textOnly"] = true;

        diagram.AddNode(top)
               .AddNode(left)
               .AddNode(right)
               .AddNode(overlap);

        _engine.Layout(diagram, _theme);

        Assert.Equal(0, overlap.Width);
        Assert.Equal(0, overlap.Height);
        Assert.True(overlap.X > left.X && overlap.X < right.X + right.Width,
            $"Expected overlap text X ({overlap.X}) to fall between the lower circles.");
        Assert.True(overlap.Y > top.Y && overlap.Y < left.Y + left.Height,
            $"Expected overlap text Y ({overlap.Y}) to fall inside the shared center region.");
    }

    [Fact]
    public void Layout_VennDiagram_PositionsTwoSetOverlapLabels()
    {
        var diagram = new Diagram { DiagramType = "venn" };

        var left = new Node("node_0", "A") { Shape = Shape.Circle };
        var right = new Node("node_1", "B") { Shape = Shape.Circle };
        var overlap = new Node("overlap_ab", "Shared");
        overlap.Metadata["venn:kind"] = "overlap";
        overlap.Metadata["venn:region"] = "ab";
        overlap.Metadata["render:textOnly"] = true;

        diagram.AddNode(left)
               .AddNode(right)
               .AddNode(overlap);

        _engine.Layout(diagram, _theme);

        Assert.True(overlap.X > left.X && overlap.X < right.X + right.Width,
            $"Expected two-set overlap label X ({overlap.X}) to fall between the circles.");
        Assert.True(overlap.Y > left.Y && overlap.Y < left.Y + left.Height,
            $"Expected two-set overlap label Y ({overlap.Y}) to remain inside the overlapping band.");
    }

    [Fact]
    public void Layout_VennDiagram_PositionsNestedTextNodesUnderSetsAndUnion()
    {
        var diagram = new Diagram { DiagramType = "venn" };

        var left = new Node("node_0", "Frontend") { Shape = Shape.Circle };
        var right = new Node("node_1", "Backend") { Shape = Shape.Circle };
        var overlap = new Node("overlap_ab", "Shared");
        overlap.Metadata["venn:kind"] = "overlap";
        overlap.Metadata["venn:region"] = "ab";
        overlap.Metadata["render:textOnly"] = true;

        var setText = new Node("A1", "React");
        setText.Metadata["venn:kind"] = "text";
        setText.Metadata["venn:parentSet"] = "node_0";
        setText.Metadata["venn:textIndex"] = 0;
        setText.Metadata["render:textOnly"] = true;

        var unionText = new Node("AB1", "OpenAPI");
        unionText.Metadata["venn:kind"] = "text";
        unionText.Metadata["venn:region"] = "ab";
        unionText.Metadata["venn:textIndex"] = 0;
        unionText.Metadata["render:textOnly"] = true;

        diagram.AddNode(left)
               .AddNode(right)
               .AddNode(overlap)
               .AddNode(setText)
               .AddNode(unionText);

        _engine.Layout(diagram, _theme);

        Assert.True(setText.Y > left.Y,
            $"Expected set text Y ({setText.Y}) to sit below the left set label area.");
        Assert.True(setText.Y < left.Y + left.Height,
            $"Expected set text Y ({setText.Y}) to remain inside the left set circle.");
        Assert.True(unionText.Y > overlap.Y,
            $"Expected union text Y ({unionText.Y}) to sit below the overlap label.");
        Assert.True(unionText.X > left.X && unionText.X < right.X + right.Width,
            $"Expected union text X ({unionText.X}) to remain inside the shared overlap band.");
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

    [Fact]
    public void Layout_BlockDiagram_UsesGridColumnsAndSpans()
    {
        var diagram = new Diagram { DiagramType = "block" };
        diagram.Metadata["block:columnCount"] = 3;

        var a = new Node("A");
        a.Metadata["block:row"] = 0;
        a.Metadata["block:column"] = 0;
        a.Metadata["block:span"] = 1;

        var b = new Node("B");
        b.Metadata["block:row"] = 0;
        b.Metadata["block:column"] = 1;
        b.Metadata["block:span"] = 2;

        var c = new Node("C");
        c.Metadata["block:row"] = 1;
        c.Metadata["block:column"] = 0;
        c.Metadata["block:span"] = 1;

        diagram.AddNode(a).AddNode(b).AddNode(c);

        _engine.Layout(diagram, _theme);

        Assert.Equal(a.Y, b.Y);
        Assert.True(c.Y > a.Y, $"Expected row 1 node Y {c.Y} to be greater than row 0 node Y {a.Y}");
        Assert.True(b.Width > a.Width, $"Expected spanning node width {b.Width} to exceed single-slot node width {a.Width}");
    }

    [Fact]
    public void Layout_BlockDiagram_SpaceGapMovesNodeToLaterColumn()
    {
        var diagram = new Diagram { DiagramType = "block" };
        diagram.Metadata["block:columnCount"] = 3;

        var left = new Node("left");
        left.Metadata["block:row"] = 0;
        left.Metadata["block:column"] = 0;
        left.Metadata["block:span"] = 1;

        var right = new Node("right");
        right.Metadata["block:row"] = 0;
        right.Metadata["block:column"] = 2;
        right.Metadata["block:span"] = 1;

        diagram.AddNode(left).AddNode(right);

        _engine.Layout(diagram, _theme);

        Assert.True(right.X > left.X + left.Width + diagram.LayoutHints.HorizontalSpacing,
            $"Expected right node X {right.X} to reflect a skipped middle column after left node right edge {left.X + left.Width}");
    }

    // ── Architecture diagram layout ───────────────────────────────────────────

    private static Diagram BuildArchitectureDiagram(Action<Diagram> configure)
    {
        var diagram = new Diagram { DiagramType = "architecture" };
        configure(diagram);
        return diagram;
    }

    private static Edge ArchEdge(string srcId, string srcPort, string dstPort, string dstId, bool directed = false)
    {
        var edge = new Edge(srcId, dstId)
        {
            ArrowHead = directed ? ArrowHeadStyle.Arrow : ArrowHeadStyle.None,
        };
        edge.Metadata["source:port"] = srcPort;
        edge.Metadata["target:port"] = dstPort;
        return edge;
    }

    [Fact]
    public void Layout_Architecture_LREdge_PlacesSourceLeftOfTarget()
    {
        // db:R -- L:server → db is left of server (same row)
        var diagram = BuildArchitectureDiagram(d =>
            d.AddNode(new Node("db", "Database"))
             .AddNode(new Node("server", "Server"))
             .AddEdge(ArchEdge("db", "R", "L", "server")));

        _engine.Layout(diagram, _theme);

        var db = diagram.Nodes["db"];
        var srv = diagram.Nodes["server"];

        Assert.True(db.X < srv.X, $"db.X ({db.X}) should be left of server.X ({srv.X}) for R--L edge");
        // Same row: Y coordinates should be equal
        Assert.Equal(db.Y, srv.Y);
    }

    [Fact]
    public void Layout_Architecture_TBEdge_PlacesSourceAboveTarget()
    {
        // disk:B -- T:server → disk is above server (same column)
        var diagram = BuildArchitectureDiagram(d =>
            d.AddNode(new Node("disk", "Storage"))
             .AddNode(new Node("server", "Server"))
             .AddEdge(ArchEdge("disk", "B", "T", "server")));

        _engine.Layout(diagram, _theme);

        var disk = diagram.Nodes["disk"];
        var srv = diagram.Nodes["server"];

        Assert.True(disk.Y < srv.Y, $"disk.Y ({disk.Y}) should be above server.Y ({srv.Y}) for B--T edge");
        // Same column: X coordinates should be equal
        Assert.Equal(disk.X, srv.X);
    }

    [Fact]
    public void Layout_Architecture_DisconnectedNode_PlacedInSeparateRow()
    {
        // 'loner' has no port edges; it should end up in a different row from the connected pair
        var diagram = BuildArchitectureDiagram(d =>
            d.AddNode(new Node("a", "A"))
             .AddNode(new Node("b", "B"))
             .AddNode(new Node("loner", "Loner"))
             .AddEdge(ArchEdge("a", "R", "L", "b")));

        _engine.Layout(diagram, _theme);

        var a = diagram.Nodes["a"];
        var loner = diagram.Nodes["loner"];

        // The disconnected node must be placed in a distinct row (different Y)
        Assert.NotEqual(a.Y, loner.Y);
    }

    [Fact]
    public void Layout_Architecture_AllPositionsNonNegative()
    {
        var diagram = BuildArchitectureDiagram(d =>
            d.AddNode(new Node("db", "Database"))
             .AddNode(new Node("server", "Server"))
             .AddEdge(ArchEdge("db", "R", "L", "server")));

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Nodes.Values, n =>
        {
            Assert.True(n.X >= 0, $"{n.Id}.X = {n.X}");
            Assert.True(n.Y >= 0, $"{n.Id}.Y = {n.Y}");
        });
    }

    [Fact]
    public void Layout_Architecture_WithGroup_GroupEnclosesMembers()
    {
        var diagram = BuildArchitectureDiagram(d =>
        {
            d.AddNode(new Node("db", "Database"));
            d.AddNode(new Node("server", "Server"));
            d.AddEdge(ArchEdge("db", "R", "L", "server"));
            var group = new Group("api", "API");
            group.ChildNodeIds.AddRange(["db", "server"]);
            d.AddGroup(group);
        });

        _engine.Layout(diagram, _theme);

        var group = diagram.Groups[0];
        foreach (var n in diagram.Nodes.Values)
        {
            Assert.True(group.X <= n.X, $"group.X ({group.X}) should be <= {n.Id}.X ({n.X})");
            Assert.True(group.Y <= n.Y, $"group.Y ({group.Y}) should be <= {n.Id}.Y ({n.Y})");
            Assert.True(group.X + group.Width >= n.X + n.Width, "group right edge should cover node right edge");
            Assert.True(group.Y + group.Height >= n.Y + n.Height, "group bottom edge should cover node bottom edge");
        }
    }
}
