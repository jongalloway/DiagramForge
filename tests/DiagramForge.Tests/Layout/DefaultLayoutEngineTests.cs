using DiagramForge.Layout;
using DiagramForge.Models;
using DiagramForge.Rendering;

namespace DiagramForge.Tests.Layout;

public class DefaultLayoutEngineTests
{
    private readonly DefaultLayoutEngine _engine = new();
    private readonly Theme _theme = Theme.Default;

    private static Node MatrixCell(string id, string label, int row, int column)
    {
        var node = new Node(id, label);
        node.Metadata["matrix:row"] = row;
        node.Metadata["matrix:column"] = column;
        return node;
    }

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
    public void Layout_VeryLongLabel_CapsWidthAndWrapsLines()
    {
        var diagram = new Diagram()
            .AddNode(new Node("long", "This is a deliberately long label that should wrap instead of producing an absurdly wide node box in the rendered diagram"));

        _engine.Layout(diagram, _theme);

        var node = diagram.Nodes["long"];
        Assert.True(node.Width <= diagram.LayoutHints.MaxNodeWidth, $"node width {node.Width} should be <= max width {diagram.LayoutHints.MaxNodeWidth}");
        Assert.True(node.Height > diagram.LayoutHints.MinNodeHeight, $"node height {node.Height} should exceed min height {diagram.LayoutHints.MinNodeHeight}");
        Assert.True(node.Label.Lines?.Length > 1, "long label should wrap into multiple lines");
    }

    [Fact]
    public void Layout_WrappedNode_DoesNotOverlapNextLayer_Vertical()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("a", "This is a deliberately long label that should wrap across multiple lines"))
               .AddNode(new Node("b", "Next"))
               .AddEdge(new Edge("a", "b"));

        _engine.Layout(diagram, _theme);

        var a = diagram.Nodes["a"];
        var b = diagram.Nodes["b"];
        Assert.True(b.Y >= a.Y + a.Height + diagram.LayoutHints.VerticalSpacing,
            $"node b at {b.Y} should be below node a bottom {a.Y + a.Height} plus spacing {diagram.LayoutHints.VerticalSpacing}");
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
    public void Layout_MatrixDiagram_PlacesQuadrantsInTwoByTwoGrid()
    {
        var diagram = new Diagram { DiagramType = "matrix" };

        diagram.AddNode(MatrixCell("cell_0_0", "Urgent\nImportant", 0, 0))
               .AddNode(MatrixCell("cell_0_1", "Not Urgent\nImportant", 0, 1))
               .AddNode(MatrixCell("cell_1_0", "Urgent\nNot Important", 1, 0))
               .AddNode(MatrixCell("cell_1_1", "Not Urgent\nNot Important", 1, 1));

        _engine.Layout(diagram, _theme);

        var topLeft = diagram.Nodes["cell_0_0"];
        var topRight = diagram.Nodes["cell_0_1"];
        var bottomLeft = diagram.Nodes["cell_1_0"];
        var bottomRight = diagram.Nodes["cell_1_1"];

        Assert.Equal(topLeft.Width, topRight.Width);
        Assert.Equal(topLeft.Height, bottomLeft.Height);
        Assert.True(topLeft.X < topRight.X);
        Assert.True(topLeft.Y < bottomLeft.Y);
        Assert.True(bottomLeft.X < bottomRight.X);
        Assert.Equal(topLeft.Width / 2, Convert.ToDouble(topLeft.Metadata["label:centerX"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(topLeft.Height / 2, Convert.ToDouble(topLeft.Metadata["label:centerY"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Layout_MatrixDiagram_WithIcons_MakesCellsLargeEnoughForIconArea()
    {
        var diagram = new Diagram { DiagramType = "matrix" };

        var withIcon = MatrixCell("cell_0_0", "Urgent\nImportant", 0, 0);
        withIcon.ResolvedIcon = new DiagramIcon("builtin", "cloud", "0 0 24 24", "<path d=\"M0 0h24v24H0z\" />");

        diagram.AddNode(withIcon)
               .AddNode(MatrixCell("cell_0_1", "Not Urgent\nImportant", 0, 1))
               .AddNode(MatrixCell("cell_1_0", "Urgent\nNot Important", 1, 0))
               .AddNode(MatrixCell("cell_1_1", "Not Urgent\nNot Important", 1, 1));

        _engine.Layout(diagram, _theme);

        double minIconWidth = SvgNodeWriter.DefaultIconSize + (2 * _theme.NodePadding);
        double minIconHeight = SvgNodeWriter.DefaultIconSize + SvgNodeWriter.IconLabelGap;

        Assert.True(withIcon.Width >= minIconWidth,
            $"Expected icon-bearing matrix cell width {withIcon.Width} to be >= {minIconWidth}.");
        Assert.True(withIcon.Height >= diagram.LayoutHints.MinNodeHeight + minIconHeight,
            $"Expected icon-bearing matrix cell height {withIcon.Height} to include icon area of at least {minIconHeight}.");
    }

    [Fact]
    public void Layout_Pyramid_AssignsTriangularSegmentMetadata()
    {
        var diagram = new Diagram { DiagramType = "pyramid" };
        diagram.AddNode(new Node("node_0", "Vision"))
               .AddNode(new Node("node_1", "Strategy"))
               .AddNode(new Node("node_2", "Tactics"))
               .AddNode(new Node("node_3", "Execution"));

        _engine.Layout(diagram, _theme);

        var top = diagram.Nodes["node_0"];
        var second = diagram.Nodes["node_1"];
        var bottom = diagram.Nodes["node_3"];

        Assert.Equal(top.X, bottom.X);
        Assert.True(top.Y < bottom.Y);
        Assert.True(second.Y > top.Y + top.Height,
            $"Expected a visible gap between pyramid levels, but second.Y ({second.Y}) was not greater than top bottom ({top.Y + top.Height}).");
        Assert.Equal(0d, Convert.ToDouble(top.Metadata["conceptual:pyramidTopWidth"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(
            Convert.ToDouble(bottom.Metadata["conceptual:pyramidBottomWidth"], System.Globalization.CultureInfo.InvariantCulture)
            > Convert.ToDouble(top.Metadata["conceptual:pyramidBottomWidth"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.All(diagram.Nodes.Values, node => Assert.True((bool)node.Metadata["conceptual:pyramidSegment"]));
    }

    [Fact]
    public void Layout_Funnel_AssignsTrapezoidSegmentMetadata()
    {
        var diagram = new Diagram { DiagramType = "funnel" };
        diagram.AddNode(new Node("node_0", "Awareness"))
               .AddNode(new Node("node_1", "Evaluation"))
               .AddNode(new Node("node_2", "Conversion"));

        _engine.Layout(diagram, _theme);

        var top = diagram.Nodes["node_0"];
        var bottom = diagram.Nodes["node_2"];

        Assert.Equal(top.X, bottom.X);
        Assert.True(top.Y < bottom.Y);
        Assert.All(diagram.Nodes.Values, node => Assert.True((bool)node.Metadata["conceptual:funnelSegment"]));
    }

    [Fact]
    public void Layout_Funnel_ProgressivelyNarrows()
    {
        var diagram = new Diagram { DiagramType = "funnel" };
        diagram.AddNode(new Node("node_0", "Awareness"))
               .AddNode(new Node("node_1", "Evaluation"))
               .AddNode(new Node("node_2", "Conversion"));

        _engine.Layout(diagram, _theme);

        var top = diagram.Nodes["node_0"];
        var middle = diagram.Nodes["node_1"];
        var bottom = diagram.Nodes["node_2"];

        double topTopWidth = Convert.ToDouble(top.Metadata["conceptual:funnelTopWidth"], System.Globalization.CultureInfo.InvariantCulture);
        double topBottomWidth = Convert.ToDouble(top.Metadata["conceptual:funnelBottomWidth"], System.Globalization.CultureInfo.InvariantCulture);
        double middleTopWidth = Convert.ToDouble(middle.Metadata["conceptual:funnelTopWidth"], System.Globalization.CultureInfo.InvariantCulture);
        double bottomBottomWidth = Convert.ToDouble(bottom.Metadata["conceptual:funnelBottomWidth"], System.Globalization.CultureInfo.InvariantCulture);

        // Top of funnel is widest
        Assert.True(topTopWidth > topBottomWidth, "Top segment should narrow from top to bottom");
        Assert.True(topTopWidth > middleTopWidth, "Second segment should be narrower than first");
        Assert.True(middleTopWidth > bottomBottomWidth, "Bottom of funnel should be narrowest");
    }

    [Fact]
    public void Layout_Funnel_FirstSegmentHasFullWidth()
    {
        var diagram = new Diagram { DiagramType = "funnel" };
        diagram.AddNode(new Node("node_0", "Awareness"))
               .AddNode(new Node("node_1", "Evaluation"))
               .AddNode(new Node("node_2", "Conversion"));

        _engine.Layout(diagram, _theme);

        var top = diagram.Nodes["node_0"];
        double topTopWidth = Convert.ToDouble(top.Metadata["conceptual:funnelTopWidth"], System.Globalization.CultureInfo.InvariantCulture);

        // The top segment's top width should equal the node bounding box width (full width)
        Assert.Equal(top.Width, topTopWidth, precision: 6);
    }

    [Fact]
    public void Layout_Funnel_VerticalOrderMatchesNodeIdOrder()
    {
        var diagram = new Diagram { DiagramType = "funnel" };
        diagram.AddNode(new Node("node_0", "Awareness"))
               .AddNode(new Node("node_1", "Evaluation"))
               .AddNode(new Node("node_2", "Conversion"));

        _engine.Layout(diagram, _theme);

        double y0 = diagram.Nodes["node_0"].Y;
        double y1 = diagram.Nodes["node_1"].Y;
        double y2 = diagram.Nodes["node_2"].Y;

        Assert.True(y0 < y1, $"node_0.Y ({y0}) should be above node_1.Y ({y1})");
        Assert.True(y1 < y2, $"node_1.Y ({y1}) should be above node_2.Y ({y2})");
    }

    [Fact]
    public void Layout_Funnel_AdjacentSegmentsHaveGapBetweenThem()
    {
        var diagram = new Diagram { DiagramType = "funnel" };
        diagram.AddNode(new Node("node_0", "Awareness"))
               .AddNode(new Node("node_1", "Evaluation"))
               .AddNode(new Node("node_2", "Conversion"));

        _engine.Layout(diagram, _theme);

        var first = diagram.Nodes["node_0"];
        var second = diagram.Nodes["node_1"];

        Assert.True(second.Y > first.Y + first.Height,
            $"Expected a gap between funnel stages, but node_1.Y ({second.Y}) was not greater than node_0 bottom ({first.Y + first.Height}).");
    }

    [Fact]
    public void Layout_Funnel_AdjacentSegmentsConnectContinuously()
    {
        // Bottom width of stage N must equal top width of stage N+1 so the taper
        // is a straight line across the gap rather than a staircase step.
        var diagram = new Diagram { DiagramType = "funnel" };
        diagram.AddNode(new Node("node_0", "Awareness"))
               .AddNode(new Node("node_1", "Evaluation"))
               .AddNode(new Node("node_2", "Conversion"));

        _engine.Layout(diagram, _theme);

        double topBottom = Convert.ToDouble(diagram.Nodes["node_0"].Metadata["conceptual:funnelBottomWidth"], System.Globalization.CultureInfo.InvariantCulture);
        double middleTop = Convert.ToDouble(diagram.Nodes["node_1"].Metadata["conceptual:funnelTopWidth"], System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(topBottom, middleTop, precision: 6);
    }

    [Fact]
    public void Layout_Funnel_SortsMoreThanTenStagesNumerically()
    {
        // StringComparer.Ordinal would sort node_10 between node_1 and node_2;
        // numeric-suffix ordering must produce the correct top-to-bottom sequence.
        var diagram = new Diagram { DiagramType = "funnel" };
        for (int i = 0; i < 12; i++)
            diagram.AddNode(new Node($"node_{i}", $"Stage {i}"));

        _engine.Layout(diagram, _theme);

        // node_9 must be above node_10 and node_11
        double y9 = diagram.Nodes["node_9"].Y;
        double y10 = diagram.Nodes["node_10"].Y;
        double y11 = diagram.Nodes["node_11"].Y;

        Assert.True(y9 < y10, $"node_9.Y ({y9}) should be above node_10.Y ({y10})");
        Assert.True(y10 < y11, $"node_10.Y ({y10}) should be above node_11.Y ({y11})");
    }

    [Fact]
    public void Layout_Funnel_WithTitle_OffsetsNodesDownward()
    {
        var diagramNoTitle = new Diagram { DiagramType = "funnel" };
        diagramNoTitle.AddNode(new Node("node_0", "Awareness"))
                      .AddNode(new Node("node_1", "Evaluation"))
                      .AddNode(new Node("node_2", "Conversion"));

        var diagramWithTitle = new Diagram { DiagramType = "funnel", Title = "Sales Pipeline" };
        diagramWithTitle.AddNode(new Node("node_0", "Awareness"))
                        .AddNode(new Node("node_1", "Evaluation"))
                        .AddNode(new Node("node_2", "Conversion"));

        _engine.Layout(diagramNoTitle, _theme);
        _engine.Layout(diagramWithTitle, _theme);

        double yNoTitle = diagramNoTitle.Nodes["node_0"].Y;
        double yWithTitle = diagramWithTitle.Nodes["node_0"].Y;

        Assert.True(yWithTitle > yNoTitle,
            $"Titled funnel top node Y ({yWithTitle}) should be below untitled ({yNoTitle}) to avoid title overlap.");
    }

    [Fact]
    public void Layout_PillarsDiagram_PlacesTitleNodesInConsistentColumns()
    {
        var diagram = new Diagram { DiagramType = "pillars" };

        diagram.AddNode(PillarTitle("pillar_0", "People", 0))
               .AddNode(PillarTitle("pillar_1", "Process", 1))
               .AddNode(PillarTitle("pillar_2", "Technology", 2));

        _engine.Layout(diagram, _theme);

        var p0 = diagram.Nodes["pillar_0"];
        var p1 = diagram.Nodes["pillar_1"];
        var p2 = diagram.Nodes["pillar_2"];

        // All title nodes should have equal width
        Assert.Equal(p0.Width, p1.Width);
        Assert.Equal(p1.Width, p2.Width);

        // Columns increase left-to-right
        Assert.True(p0.X < p1.X, $"Expected p0.X ({p0.X}) < p1.X ({p1.X})");
        Assert.True(p1.X < p2.X, $"Expected p1.X ({p1.X}) < p2.X ({p2.X})");

        // Column spacing is consistent
        double gap01 = p1.X - (p0.X + p0.Width);
        double gap12 = p2.X - (p1.X + p1.Width);
        Assert.Equal(gap01, gap12, precision: 2);

        // Label centers are set
        Assert.True(p0.Metadata.ContainsKey("label:centerX"));
        Assert.True(p0.Metadata.ContainsKey("label:centerY"));
    }

    [Fact]
    public void Layout_PillarsDiagram_StacksSegmentsBelowTitle()
    {
        var diagram = new Diagram { DiagramType = "pillars" };

        diagram.AddNode(PillarTitle("pillar_0", "People", 0))
               .AddNode(PillarSegment("pillar_0_segment_0", "Skills", 0, 0))
               .AddNode(PillarSegment("pillar_0_segment_1", "Roles", 0, 1))
               .AddNode(PillarTitle("pillar_1", "Process", 1))
               .AddNode(PillarSegment("pillar_1_segment_0", "Intake", 1, 0));

        _engine.Layout(diagram, _theme);

        var title0 = diagram.Nodes["pillar_0"];
        var seg0 = diagram.Nodes["pillar_0_segment_0"];
        var seg1 = diagram.Nodes["pillar_0_segment_1"];

        // Segments start below the title
        Assert.True(seg0.Y > title0.Y, $"Expected seg0.Y ({seg0.Y}) > title0.Y ({title0.Y})");
        Assert.True(seg0.Y >= title0.Y + title0.Height, $"Expected seg0 to start at or after the bottom of title0");

        // Segments stack in order
        Assert.True(seg1.Y > seg0.Y, $"Expected seg1.Y ({seg1.Y}) > seg0.Y ({seg0.Y})");

        // Segments align with their pillar column
        Assert.Equal(title0.X, seg0.X);
        Assert.Equal(title0.Width, seg0.Width);
    }

    [Fact]
    public void Layout_PillarsDiagram_AllNodesHavePositiveSize()
    {
        var diagram = new Diagram { DiagramType = "pillars" };

        diagram.AddNode(PillarTitle("pillar_0", "People", 0))
               .AddNode(PillarSegment("pillar_0_segment_0", "Skills", 0, 0))
               .AddNode(PillarTitle("pillar_1", "Process", 1))
               .AddNode(PillarSegment("pillar_1_segment_0", "Intake", 1, 0));

        _engine.Layout(diagram, _theme);

        foreach (var node in diagram.Nodes.Values)
        {
            Assert.True(node.Width > 0, $"Node '{node.Id}' Width should be positive");
            Assert.True(node.Height > 0, $"Node '{node.Id}' Height should be positive");
        }
    }

    private static Node PillarTitle(string id, string label, int pillarIndex)
    {
        var node = new Node(id, label);
        node.Metadata["pillars:pillarIndex"] = pillarIndex;
        node.Metadata["pillars:kind"] = "title";
        return node;
    }

    private static Node PillarSegment(string id, string label, int pillarIndex, int segmentIndex)
    {
        var node = new Node(id, label);
        node.Metadata["pillars:pillarIndex"] = pillarIndex;
        node.Metadata["pillars:segmentIndex"] = segmentIndex;
        node.Metadata["pillars:kind"] = "segment";
        return node;
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
    public void Layout_GroupInFirstColumn_PreservesOuterDiagramPadding()
    {
        var diagram = new Diagram().AddNode(new Node("A"));
        var group = new Group("G", "Label pushes the top up");
        group.ChildNodeIds.Add("A");
        diagram.AddGroup(group);

        _engine.Layout(diagram, _theme);

        var a = diagram.Nodes["A"];
        Assert.True(group.X >= _theme.DiagramPadding, $"group.X = {group.X}");
        Assert.True(group.Y >= _theme.DiagramPadding, $"group.Y = {group.Y}");
        Assert.True(a.X >= _theme.DiagramPadding, $"A.X = {a.X}");
        Assert.True(a.Y >= _theme.DiagramPadding, $"A.Y = {a.Y}");
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
    public void Layout_NestedGroups_ParentEnclosesChildGroup()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;

        var parent = new Group("parent", "Parent");
        parent.ChildGroupIds.Add("child");

        var child = new Group("child", "Child");
        child.ChildNodeIds.AddRange(["A", "B"]);

        diagram.AddGroup(parent);
        diagram.AddGroup(child);

        _engine.Layout(diagram, _theme);

        Assert.True(parent.X <= child.X, $"parent.X {parent.X} should be <= child.X {child.X}");
        Assert.True(parent.Y <= child.Y, $"parent.Y {parent.Y} should be <= child.Y {child.Y}");
        Assert.True(parent.X + parent.Width >= child.X + child.Width,
            $"parent right edge {parent.X + parent.Width} should cover child right edge {child.X + child.Width}");
        Assert.True(parent.Y + parent.Height >= child.Y + child.Height,
            $"parent bottom edge {parent.Y + parent.Height} should cover child bottom edge {child.Y + child.Height}");
    }

    [Fact]
    public void Layout_SubgraphLocalDirection_MembersFollowLocalDirection()
    {
        // Outer diagram is LR; Backend subgraph declares direction TB.
        // A and B must end up stacked vertically (same X, B.Y > A.Y)
        // even though the outer flow is horizontal.
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddNode(new Node("C"))
               .AddEdge(new Edge("A", "B"))
               .AddEdge(new Edge("B", "C"));
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;

        var group = new Group("Backend", "Backend") { Direction = LayoutDirection.TopToBottom };
        group.ChildNodeIds.AddRange(["A", "B"]);
        diagram.AddGroup(group);

        _engine.Layout(diagram, _theme);

        var a = diagram.Nodes["A"];
        var b = diagram.Nodes["B"];
        var c = diagram.Nodes["C"];

        // Inside the Backend group: A is above B (TB direction)
        Assert.True(a.Y < b.Y, $"With local TB: A.Y ({a.Y}) should be < B.Y ({b.Y})");

        // Inside the Backend group: A and B share the same column (same X)
        Assert.Equal(a.X, b.X);

        // The outer LR flow is preserved: C (outside the group) is to the right of the group
        Assert.True(c.X > group.X + group.Width - _theme.DiagramPadding,
            $"C.X ({c.X}) should be to the right of the group's right edge ({group.X + group.Width})");

        // Group must still enclose its members
        Assert.True(group.X < a.X);
        Assert.True(group.Y < a.Y);
        Assert.True(group.X + group.Width > b.X + b.Width);
        Assert.True(group.Y + group.Height > b.Y + b.Height);
    }

    [Fact]
    public void Layout_SubgraphLocalDirectionLR_OuterTB_MembersArrangedHorizontally()
    {
        // Outer diagram is TB; subgraph declares direction LR.
        // A and B must end up side-by-side (same Y, B.X > A.X).
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;

        var group = new Group("G", "G") { Direction = LayoutDirection.LeftToRight };
        group.ChildNodeIds.AddRange(["A", "B"]);
        diagram.AddGroup(group);

        _engine.Layout(diagram, _theme);

        var a = diagram.Nodes["A"];
        var b = diagram.Nodes["B"];

        // With local LR: A is to the left of B (A.X < B.X)
        Assert.True(a.X < b.X, $"With local LR: A.X ({a.X}) should be < B.X ({b.X})");
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

    // ── Cycle ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Layout_CycleDiagram_PlacesNodesRadially()
    {
        var diagram = new Diagram { DiagramType = "cycle" };
        diagram.AddNode(new Node("node_0", "Plan"))
               .AddNode(new Node("node_1", "Build"))
               .AddNode(new Node("node_2", "Measure"))
               .AddNode(new Node("node_3", "Learn"));

        _engine.Layout(diagram, _theme);

        // All nodes must have positive, non-overlapping positions.
        Assert.All(diagram.Nodes.Values, n =>
        {
            Assert.True(n.X >= 0, $"{n.Id}.X = {n.X}");
            Assert.True(n.Y >= 0, $"{n.Id}.Y = {n.Y}");
            Assert.True(n.Width > 0);
            Assert.True(n.Height > 0);
        });

        // Nodes should be evenly spaced: all centres equidistant from the common
        // centre of the circle, within floating-point tolerance.
        var nodes = diagram.Nodes.Values
            .OrderBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        double cx = nodes.Average(n => n.X + n.Width / 2);
        double cy = nodes.Average(n => n.Y + n.Height / 2);

        var radii = nodes.Select(n =>
        {
            double dx = n.X + n.Width / 2 - cx;
            double dy = n.Y + n.Height / 2 - cy;
            return Math.Sqrt(dx * dx + dy * dy);
        }).ToList();

        double avgRadius = radii.Average();
        Assert.All(radii, r =>
            Assert.True(Math.Abs(r - avgRadius) < 1.0,
                $"Expected all radii ≈ {avgRadius:F2} but got {r:F2}"));
    }

    [Fact]
    public void Layout_CycleDiagram_StableOrdering_StartFromTop()
    {
        // node_0 should be the topmost node (12 o'clock).
        var diagram = new Diagram { DiagramType = "cycle" };
        diagram.AddNode(new Node("node_0", "Plan"))
               .AddNode(new Node("node_1", "Build"))
               .AddNode(new Node("node_2", "Measure"));

        _engine.Layout(diagram, _theme);

        var n0 = diagram.Nodes["node_0"];
        Assert.All(diagram.Nodes.Values, n =>
            Assert.True(n0.Y <= n.Y + 1.0,
                $"node_0.Y ({n0.Y:F2}) should be the topmost node, but {n.Id}.Y = {n.Y:F2}"));
    }

    [Fact]
    public void Layout_CycleDiagram_AllNodesHaveEqualSize()
    {
        var diagram = new Diagram { DiagramType = "cycle" };
        diagram.AddNode(new Node("node_0", "Plan"))
               .AddNode(new Node("node_1", "Build"))
               .AddNode(new Node("node_2", "Measure"))
               .AddNode(new Node("node_3", "Learn"));

        _engine.Layout(diagram, _theme);

        var sizes = diagram.Nodes.Values.Select(n => (n.Width, n.Height)).Distinct().ToList();
        Assert.Single(sizes);
    }

    [Fact]
    public void Layout_CycleDiagram_TagsEdgesForCircularRouting()
    {
        var diagram = new Diagram { DiagramType = "cycle" };
        diagram.AddNode(new Node("node_0", "Plan"))
               .AddNode(new Node("node_1", "Build"))
               .AddNode(new Node("node_2", "Measure"))
               .AddNode(new Node("node_3", "Learn"))
               .AddEdge(new Edge("node_0", "node_1"))
               .AddEdge(new Edge("node_1", "node_2"))
               .AddEdge(new Edge("node_2", "node_3"))
               .AddEdge(new Edge("node_3", "node_0"));

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Edges, edge =>
        {
            Assert.True(edge.Metadata.TryGetValue("conceptual:cycleArc", out var route) && route is true);
            Assert.True(edge.Metadata.ContainsKey("cycle:centerX"));
            Assert.True(edge.Metadata.ContainsKey("cycle:centerY"));
            Assert.True(edge.Metadata.ContainsKey("cycle:radius"));
        });
    }

    // ── Chevrons ──────────────────────────────────────────────────────────────

    [Fact]
    public void Layout_Chevrons_AssignsChevronSegmentMetadata()
    {
        var diagram = new Diagram { DiagramType = "chevrons" };
        diagram.AddNode(new Node("node_0", "Discover"))
               .AddNode(new Node("node_1", "Build"))
               .AddNode(new Node("node_2", "Launch"));

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Nodes.Values, node => Assert.True((bool)node.Metadata["conceptual:chevronSegment"]));
        Assert.All(diagram.Nodes.Values, node => Assert.True(node.Metadata.ContainsKey("conceptual:chevronTipDepth")));
    }

    [Fact]
    public void Layout_Chevrons_HorizontalOrderMatchesNodeIdOrder()
    {
        var diagram = new Diagram { DiagramType = "chevrons" };
        diagram.AddNode(new Node("node_0", "Discover"))
               .AddNode(new Node("node_1", "Build"))
               .AddNode(new Node("node_2", "Launch"));

        _engine.Layout(diagram, _theme);

        double x0 = diagram.Nodes["node_0"].X;
        double x1 = diagram.Nodes["node_1"].X;
        double x2 = diagram.Nodes["node_2"].X;

        Assert.True(x0 < x1, $"node_0.X ({x0}) should be left of node_1.X ({x1})");
        Assert.True(x1 < x2, $"node_1.X ({x1}) should be left of node_2.X ({x2})");
    }

    [Fact]
    public void Layout_Chevrons_AllNodesHaveEqualSizeAndSameY()
    {
        var diagram = new Diagram { DiagramType = "chevrons" };
        diagram.AddNode(new Node("node_0", "Discover"))
               .AddNode(new Node("node_1", "Build"))
               .AddNode(new Node("node_2", "Launch"));

        _engine.Layout(diagram, _theme);

        var nodes = diagram.Nodes.Values.ToList();
        double w0 = nodes[0].Width;
        double h0 = nodes[0].Height;
        double y0 = nodes[0].Y;

        Assert.All(nodes, node =>
        {
            Assert.Equal(w0, node.Width, precision: 6);
            Assert.Equal(h0, node.Height, precision: 6);
            Assert.Equal(y0, node.Y, precision: 6);
        });
    }

    // ── Radial ────────────────────────────────────────────────────────────────

    private static Diagram BuildRadialDiagram(string center, IEnumerable<string> items)
    {
        var diagram = new Diagram { DiagramType = "radial" };
        var centerNode = new Node("center", center);
        centerNode.Metadata["radial:isCenter"] = true;
        diagram.AddNode(centerNode);

        int i = 0;
        foreach (var item in items)
        {
            var node = new Node($"item_{i}", item);
            node.Metadata["radial:itemIndex"] = i;
            diagram.AddNode(node);
            i++;
        }

        return diagram;
    }

    [Fact]
    public void Layout_RadialDiagram_CenterNodeIsCircle()
    {
        var diagram = BuildRadialDiagram("Platform", ["Security", "Reliability", "Observability", "Performance"]);

        _engine.Layout(diagram, _theme);

        var center = diagram.Nodes["center"];
        Assert.Equal(Shape.Circle, center.Shape);
        Assert.Equal(center.Width, center.Height);
    }

    [Fact]
    public void Layout_RadialDiagram_ItemNodesArePlacedRadially()
    {
        var diagram = BuildRadialDiagram("Hub", ["A", "B", "C", "D"]);

        _engine.Layout(diagram, _theme);

        var center = diagram.Nodes["center"];
        double cx = center.X + center.Width / 2;
        double cy = center.Y + center.Height / 2;

        var itemNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.ContainsKey("radial:itemIndex"))
            .ToList();

        var radii = itemNodes.Select(n =>
        {
            double dx = n.X + n.Width / 2 - cx;
            double dy = n.Y + n.Height / 2 - cy;
            return Math.Sqrt(dx * dx + dy * dy);
        }).ToList();

        double avgRadius = radii.Average();
        Assert.All(radii, r =>
            Assert.True(Math.Abs(r - avgRadius) < 1.0,
                $"Expected all item radii ≈ {avgRadius:F2} but got {r:F2}"));
    }

    [Fact]
    public void Layout_RadialDiagram_CenterNodeIsAtCanvasCenter()
    {
        var diagram = BuildRadialDiagram("Hub", ["A", "B", "C", "D"]);

        _engine.Layout(diagram, _theme);

        var center = diagram.Nodes["center"];
        var items = diagram.Nodes.Values.Where(n => n.Metadata.ContainsKey("radial:itemIndex")).ToList();

        double avgItemX = items.Average(n => n.X + n.Width / 2);
        double avgItemY = items.Average(n => n.Y + n.Height / 2);
        double centerX = center.X + center.Width / 2;
        double centerY = center.Y + center.Height / 2;

        Assert.True(Math.Abs(centerX - avgItemX) < 1.0,
            $"Center X ({centerX:F2}) should align with average item X ({avgItemX:F2})");
        Assert.True(Math.Abs(centerY - avgItemY) < 1.0,
            $"Center Y ({centerY:F2}) should align with average item Y ({avgItemY:F2})");
    }

    [Fact]
    public void Layout_RadialDiagram_FirstItemIsAtTop()
    {
        var diagram = BuildRadialDiagram("Hub", ["Top", "B", "C", "D"]);

        _engine.Layout(diagram, _theme);

        var item0 = diagram.Nodes["item_0"];
        var otherItems = diagram.Nodes.Values
            .Where(n => n.Metadata.ContainsKey("radial:itemIndex") && n.Id != "item_0")
            .ToList();

        Assert.All(otherItems, n =>
            Assert.True(item0.Y <= n.Y + 1.0,
                $"item_0.Y ({item0.Y:F2}) should be topmost but {n.Id}.Y = {n.Y:F2}"));
    }

    [Fact]
    public void Layout_RadialDiagram_AllNodesHavePositiveSize()
    {
        var diagram = BuildRadialDiagram("Hub", ["A", "B", "C"]);

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Nodes.Values, n =>
        {
            Assert.True(n.Width > 0, $"{n.Id}.Width = {n.Width}");
            Assert.True(n.Height > 0, $"{n.Id}.Height = {n.Height}");
        });
    }

    [Fact]
    public void Layout_RadialDiagram_AllPositionsNonNegative()
    {
        var diagram = BuildRadialDiagram("Hub", ["A", "B", "C"]);

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Nodes.Values, n =>
        {
            Assert.True(n.X >= 0, $"{n.Id}.X = {n.X}");
            Assert.True(n.Y >= 0, $"{n.Id}.Y = {n.Y}");
        });
    }

    [Fact]
    public void Layout_Chevrons_FirstNodeHasNoLeftNotchByIndex()
    {
        var diagram = new Diagram { DiagramType = "chevrons" };
        diagram.AddNode(new Node("node_0", "First"))
               .AddNode(new Node("node_1", "Second"));

        _engine.Layout(diagram, _theme);

        Assert.Equal(0, diagram.Nodes["node_0"].Metadata["conceptual:chevronIndex"]);
        Assert.Equal(1, diagram.Nodes["node_1"].Metadata["conceptual:chevronIndex"]);
    }

    [Fact]
    public void Layout_Chevrons_WithTitle_OffsetsNodesDownward()
    {
        var diagramNoTitle = new Diagram { DiagramType = "chevrons" };
        diagramNoTitle.AddNode(new Node("node_0", "A"))
                      .AddNode(new Node("node_1", "B"));

        var diagramWithTitle = new Diagram { DiagramType = "chevrons", Title = "Process" };
        diagramWithTitle.AddNode(new Node("node_0", "A"))
                        .AddNode(new Node("node_1", "B"));

        _engine.Layout(diagramNoTitle, _theme);
        _engine.Layout(diagramWithTitle, _theme);

        double yNoTitle = diagramNoTitle.Nodes["node_0"].Y;
        double yWithTitle = diagramWithTitle.Nodes["node_0"].Y;

        Assert.True(yWithTitle > yNoTitle,
            $"Titled chevron top node Y ({yWithTitle}) should be below untitled ({yNoTitle}) to avoid title overlap.");
    }

    [Fact]
    public void Layout_Chevrons_AdjacentNodesAreImmediatelyAbutted()
    {
        var diagram = new Diagram { DiagramType = "chevrons" };
        diagram.AddNode(new Node("node_0", "Discover"))
               .AddNode(new Node("node_1", "Build"));

        _engine.Layout(diagram, _theme);

        var first = diagram.Nodes["node_0"];
        var second = diagram.Nodes["node_1"];

        // Overlap layout: the right tip of stage 0 aligns with the inward notch of stage 1.
        // second.X is inset by tipDepth so the tip (first.X + first.Width) coincides with
        // the notch vertex (second.X + tipDepth).
        double tipDepth = (double)second.Metadata["conceptual:chevronTipDepth"];
        Assert.Equal(first.X + first.Width, second.X + tipDepth, precision: 6);
    }

    [Fact]
    public void Layout_Chevrons_StoresChevronCount()
    {
        var diagram = new Diagram { DiagramType = "chevrons" };
        diagram.AddNode(new Node("node_0", "A"))
               .AddNode(new Node("node_1", "B"))
               .AddNode(new Node("node_2", "C"));

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Nodes.Values, node => Assert.Equal(3, node.Metadata["conceptual:chevronCount"]));
    }

    // ── Tree ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Layout_Tree_ChildrenCenteredUnderParent()
    {
        var diagram = new Diagram { DiagramType = "tree" };
        var root = new Node("node_0", "Root");
        root.Metadata["tree:depth"] = 0;
        var child1 = new Node("node_1", "Child1");
        child1.Metadata["tree:depth"] = 1;
        var child2 = new Node("node_2", "Child2");
        child2.Metadata["tree:depth"] = 1;
        diagram.AddNode(root)
               .AddNode(child1)
               .AddNode(child2)
               .AddEdge(new Edge("node_0", "node_1"))
               .AddEdge(new Edge("node_0", "node_2"));

        _engine.Layout(diagram, _theme);

        // Root should be centered over its children
        double rootCenter = root.X + root.Width / 2;
        double childrenCenter = (child1.X + child1.Width / 2 + child2.X + child2.Width / 2) / 2;
        Assert.True(Math.Abs(rootCenter - childrenCenter) < 1.0,
            $"Root center ({rootCenter}) should be close to children center ({childrenCenter}).");
    }

    [Fact]
    public void Layout_Tree_ChildrenBelowParent()
    {
        var diagram = new Diagram { DiagramType = "tree" };
        var root = new Node("node_0", "Root");
        root.Metadata["tree:depth"] = 0;
        var child = new Node("node_1", "Child");
        child.Metadata["tree:depth"] = 1;
        diagram.AddNode(root)
               .AddNode(child)
               .AddEdge(new Edge("node_0", "node_1"));

        _engine.Layout(diagram, _theme);

        Assert.True(child.Y > root.Y,
            $"Child Y ({child.Y}) should be below root Y ({root.Y}).");
    }

    [Fact]
    public void Layout_Tree_MultiRoot_SideBySide()
    {
        var diagram = new Diagram { DiagramType = "tree" };
        var root1 = new Node("node_0", "Root1");
        root1.Metadata["tree:depth"] = 0;
        var root2 = new Node("node_1", "Root2");
        root2.Metadata["tree:depth"] = 0;
        diagram.AddNode(root1)
               .AddNode(root2);

        _engine.Layout(diagram, _theme);

        // Both roots at the same Y level
        Assert.Equal(root1.Y, root2.Y, precision: 1);
        // Root2 is to the right of Root1
        Assert.True(root2.X > root1.X,
            $"Root2 X ({root2.X}) should be to the right of Root1 X ({root1.X}).");
    }

    [Fact]
    public void Layout_Tree_AllNodesHavePositiveDimensions()
    {
        var diagram = new Diagram { DiagramType = "tree" };
        var root = new Node("node_0", "Root");
        root.Metadata["tree:depth"] = 0;
        var child = new Node("node_1", "Child");
        child.Metadata["tree:depth"] = 1;
        diagram.AddNode(root)
               .AddNode(child)
               .AddEdge(new Edge("node_0", "node_1"));

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Nodes.Values, node =>
        {
            Assert.True(node.Width > 0, $"Node {node.Id} Width should be > 0");
            Assert.True(node.Height > 0, $"Node {node.Id} Height should be > 0");
            Assert.True(node.X >= 0, $"Node {node.Id} X should be >= 0");
            Assert.True(node.Y >= 0, $"Node {node.Id} Y should be >= 0");
        });
    }

    // ── Snake ─────────────────────────────────────────────────────────────────

    private static Diagram CreateSnakeDiagram(int stepCount, bool withDescriptions = false)
    {
        var diagram = new Diagram { DiagramType = "snake", SourceSyntax = "conceptual" };
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;

        for (int i = 0; i < stepCount; i++)
        {
            var node = new Node($"node_{i}", $"Step {i + 1}");
            node.Metadata["snake:stepIndex"] = i;
            if (withDescriptions)
                node.Metadata["snake:description"] = $"Description for step {i + 1}";
            diagram.AddNode(node);
        }

        return diagram;
    }

    [Fact]
    public void Layout_Snake_AllNodesGetPositiveSize()
    {
        var diagram = CreateSnakeDiagram(5);

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Nodes.Values, node =>
        {
            Assert.True(node.Width > 0, $"Node {node.Id} Width should be > 0");
            Assert.True(node.Height > 0, $"Node {node.Id} Height should be > 0");
        });
    }

    [Fact]
    public void Layout_Snake_NodesArrangedLeftToRight()
    {
        var diagram = CreateSnakeDiagram(5);

        _engine.Layout(diagram, _theme);

        var ordered = diagram.Nodes.Values
            .OrderBy(n => (int)n.Metadata["snake:stepIndex"])
            .ToList();

        for (int i = 1; i < ordered.Count; i++)
        {
            Assert.True(ordered[i].X > ordered[i - 1].X,
                $"Node {ordered[i].Id}.X ({ordered[i].X}) should be > {ordered[i - 1].Id}.X ({ordered[i - 1].X})");
        }
    }

    [Fact]
    public void Layout_Snake_NodesAreCircles()
    {
        var diagram = CreateSnakeDiagram(4);

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Nodes.Values, node =>
        {
            Assert.Equal(Shape.Circle, node.Shape);
        });
    }

    [Fact]
    public void Layout_Snake_StoresPathDataInMetadata()
    {
        var diagram = CreateSnakeDiagram(4);

        _engine.Layout(diagram, _theme);

        Assert.True(diagram.Metadata.ContainsKey("snake:pathData"));
        var pathData = diagram.Metadata["snake:pathData"] as string;
        Assert.NotNull(pathData);
        Assert.StartsWith("M ", pathData);
        Assert.Contains("A ", pathData);
    }

    [Fact]
    public void Layout_Snake_StoresStrokeWidthInMetadata()
    {
        var diagram = CreateSnakeDiagram(3);

        _engine.Layout(diagram, _theme);

        Assert.True(diagram.Metadata.ContainsKey("snake:strokeWidth"));
        var strokeWidth = Convert.ToDouble(diagram.Metadata["snake:strokeWidth"]);
        Assert.True(strokeWidth > 0);
    }

    [Fact]
    public void Layout_Snake_StoresSegmentColorsInMetadata()
    {
        var diagram = CreateSnakeDiagram(5);

        _engine.Layout(diagram, _theme);

        Assert.True(diagram.Metadata.ContainsKey("snake:segmentColors"));
        var colors = diagram.Metadata["snake:segmentColors"] as List<string>;
        Assert.NotNull(colors);
        Assert.Equal(5, colors.Count); // one color per circle
    }

    [Fact]
    public void Layout_Snake_WithDescriptions_StoresDescriptionPositions()
    {
        var diagram = CreateSnakeDiagram(4, withDescriptions: true);

        _engine.Layout(diagram, _theme);

        foreach (var node in diagram.Nodes.Values)
        {
            Assert.True(node.Metadata.ContainsKey("snake:descX"));
            Assert.True(node.Metadata.ContainsKey("snake:descY"));
            Assert.True(node.Metadata.ContainsKey("snake:descBelow"));
        }
    }

    [Fact]
    public void Layout_Snake_DescriptionsAlternateBelowAndAbove()
    {
        var diagram = CreateSnakeDiagram(4, withDescriptions: true);

        _engine.Layout(diagram, _theme);

        var ordered = diagram.Nodes.Values
            .OrderBy(n => (int)n.Metadata["snake:stepIndex"])
            .ToList();

        // Even index = below, Odd index = above
        Assert.True((bool)ordered[0].Metadata["snake:descBelow"]);
        Assert.False((bool)ordered[1].Metadata["snake:descBelow"]);
        Assert.True((bool)ordered[2].Metadata["snake:descBelow"]);
        Assert.False((bool)ordered[3].Metadata["snake:descBelow"]);
    }

    [Fact]
    public void Layout_Snake_AssignsFillColorsFromPalette()
    {
        var diagram = CreateSnakeDiagram(3);

        _engine.Layout(diagram, _theme);

        Assert.All(diagram.Nodes.Values, node =>
        {
            Assert.NotNull(node.FillColor);
            Assert.NotNull(node.StrokeColor);
        });
    }

    [Fact]
    public void Layout_Snake_PrismTheme_ProducesVisibleNonWhiteFillColors()
    {
        // Prism has an all-white NodePalette. The layout must fall back to a
        // chromatic palette so circles and tube segments are not invisible.
        var diagram = CreateSnakeDiagram(5);

        _engine.Layout(diagram, Theme.Prism);

        // Each node fill must differ from the white Prism background.
        Assert.All(diagram.Nodes.Values, node =>
        {
            Assert.NotNull(node.FillColor);
            Assert.NotEqual(Theme.Prism.BackgroundColor, node.FillColor, StringComparer.OrdinalIgnoreCase);
        });

        // Segment colors stored in metadata must also be non-white.
        var segmentColors = diagram.Metadata["snake:segmentColors"] as List<string>;
        Assert.NotNull(segmentColors);
        Assert.True(segmentColors.Count > 0);
        Assert.False(ColorUtils.IsPaletteMonochrome(segmentColors, Theme.Prism.BackgroundColor),
            "Segment colors must not be monochrome relative to the Prism background.");
    }

    // ── TabList ───────────────────────────────────────────────────────────────

    private static Diagram CreateTabListDiagram(string layout, int categoryCount = 3, int itemsPerCategory = 2)
    {
        var diagram = new Diagram { DiagramType = "tablist", SourceSyntax = "conceptual" };

        for (int c = 0; c < categoryCount; c++)
        {
            var titleNode = new Node($"tab_{c}", $"Category {c + 1}");
            titleNode.Metadata["tablist:kind"] = "title";
            titleNode.Metadata["tablist:categoryIndex"] = c;
            titleNode.Metadata["tablist:layout"] = layout;
            diagram.AddNode(titleNode);

            for (int i = 0; i < itemsPerCategory; i++)
            {
                var itemNode = new Node($"tab_{c}_item_{i}", $"Item {c + 1}.{i + 1}");
                itemNode.Metadata["tablist:kind"] = "item";
                itemNode.Metadata["tablist:categoryIndex"] = c;
                itemNode.Metadata["tablist:itemIndex"] = i;
                itemNode.Metadata["tablist:layout"] = layout;
                diagram.AddNode(itemNode);
            }
        }

        return diagram;
    }

    [Theory]
    [InlineData("cards")]
    [InlineData("stacked")]
    [InlineData("flat")]
    public void Layout_TabList_AllVariants_TitleNodesGetPositiveSize(string layout)
    {
        var diagram = CreateTabListDiagram(layout);

        _engine.Layout(diagram, _theme);

        var titleNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("tablist:kind", out var k) && "title".Equals(k as string, StringComparison.Ordinal))
            .ToList();

        Assert.True(titleNodes.Count > 0);
        Assert.All(titleNodes, n =>
        {
            Assert.True(n.Width > 0, $"Title node {n.Id} Width should be > 0");
            Assert.True(n.Height > 0, $"Title node {n.Id} Height should be > 0");
        });
    }

    [Theory]
    [InlineData("cards")]
    [InlineData("stacked")]
    [InlineData("flat")]
    public void Layout_TabList_PrismTheme_TitleNodesHaveChromaticFills(string layout)
    {
        // Prism has an all-white NodePalette. The layout must fall back to a
        // chromatic palette so accent fills, tab fills, and bar fills are visible.
        var diagram = CreateTabListDiagram(layout);

        _engine.Layout(diagram, Theme.Prism);

        var titleNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("tablist:kind", out var k) && "title".Equals(k as string, StringComparison.Ordinal))
            .ToList();

        Assert.True(titleNodes.Count > 0);

        // Each title node's fill must differ from the white Prism background.
        Assert.All(titleNodes, n =>
        {
            Assert.NotNull(n.FillColor);
            Assert.NotEqual(Theme.Prism.BackgroundColor, n.FillColor, StringComparer.OrdinalIgnoreCase);
        });

        // The set of fill colors across title nodes must not be monochrome.
        var fills = titleNodes.Select(n => n.FillColor!).ToList();
        Assert.False(ColorUtils.IsPaletteMonochrome(fills, Theme.Prism.BackgroundColor),
            $"TabList '{layout}' fill colors must not be monochrome with Prism theme.");
    }

    [Theory]
    [InlineData("cards")]
    [InlineData("stacked")]
    [InlineData("flat")]
    public void Layout_TabList_PrismTheme_AccentAndContentColorsAreChromaticInMetadata(string layout)
    {
        // The layout stores accent/content colors in node metadata; these must also
        // be chromatic so the rendered output is visible against Prism's white background.
        var diagram = CreateTabListDiagram(layout);

        _engine.Layout(diagram, Theme.Prism);

        var titleNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("tablist:kind", out var k) && "title".Equals(k as string, StringComparison.Ordinal))
            .ToList();

        Assert.True(titleNodes.Count > 0);

        foreach (var n in titleNodes)
        {
            // contentFill is required for cards and stacked; optional (not expected) for flat.
            if (layout is "cards" or "stacked")
            {
                Assert.True(n.Metadata.TryGetValue("tablist:contentFill", out var requiredCf) && requiredCf is string,
                    $"tablist:contentFill must be set for '{layout}' layout.");
                var requiredContentFill = n.Metadata["tablist:contentFill"] as string;
                Assert.NotEqual(Theme.Prism.BackgroundColor, requiredContentFill, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                if (n.Metadata.TryGetValue("tablist:contentFill", out var cf) && cf is string contentFill)
                    Assert.NotEqual(Theme.Prism.BackgroundColor, contentFill, StringComparer.OrdinalIgnoreCase);
            }

            // accentColor is required for flat layout; optional for others.
            if (string.Equals(layout, "flat", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(n.Metadata.TryGetValue("tablist:accentColor", out var requiredAc) && requiredAc is string,
                    "tablist:accentColor must be set for 'flat' layout.");
                var requiredAccentColor = n.Metadata["tablist:accentColor"] as string;
                Assert.NotEqual(Theme.Prism.BackgroundColor, requiredAccentColor, StringComparer.OrdinalIgnoreCase);
            }
            else if (n.Metadata.TryGetValue("tablist:accentColor", out var ac) && ac is string accentColor)
            {
                Assert.NotEqual(Theme.Prism.BackgroundColor, accentColor, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [InlineData("cards")]
    [InlineData("stacked")]
    [InlineData("flat")]
    public void Layout_TabList_DefaultTheme_IsUnaffected(string layout)
    {
        // Non-Prism themes with chromatic palettes must continue to work as before.
        var diagram = CreateTabListDiagram(layout);

        _engine.Layout(diagram, Theme.Default);

        var titleNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("tablist:kind", out var k) && "title".Equals(k as string, StringComparison.Ordinal))
            .ToList();

        Assert.True(titleNodes.Count > 0);
        Assert.All(titleNodes, n =>
        {
            Assert.NotNull(n.FillColor);
            Assert.True(n.Width > 0);
            Assert.True(n.Height > 0);
        });
    }

    // ── Sequence diagram heading offset ───────────────────────────────────────

    private static Diagram BuildSequenceDiagram(string? title = null, string? subtitle = null)
    {
        var diagram = new Diagram
        {
            SourceSyntax = "mermaid",
            DiagramType = "sequencediagram",
            Title = title,
            Subtitle = subtitle,
        };
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;

        var nodeA = new Node("A", "Alice") { Shape = Shape.Rectangle };
        nodeA.Metadata["sequence:participantIndex"] = 0;
        var nodeB = new Node("B", "Bob") { Shape = Shape.Rectangle };
        nodeB.Metadata["sequence:participantIndex"] = 1;
        diagram.AddNode(nodeA);
        diagram.AddNode(nodeB);

        var edge = new Edge("A", "B");
        edge.Metadata["sequence:messageIndex"] = 0;
        diagram.AddEdge(edge);

        return diagram;
    }

    [Fact]
    public void Layout_SequenceDiagram_NoHeading_NodesStartAtPad()
    {
        var diagram = BuildSequenceDiagram();

        _engine.Layout(diagram, _theme);

        double pad = _theme.DiagramPadding;
        foreach (var node in diagram.Nodes.Values)
            Assert.True(node.Y >= pad, $"Node Y ({node.Y}) should be >= pad ({pad})");
    }

    [Fact]
    public void Layout_SequenceDiagram_WithTitle_NodesShiftedDown()
    {
        var withoutTitle = BuildSequenceDiagram();
        var withTitle = BuildSequenceDiagram(title: "My Title");

        _engine.Layout(withoutTitle, _theme);
        _engine.Layout(withTitle, _theme);

        double noTitleY = withoutTitle.Nodes["A"].Y;
        double withTitleY = withTitle.Nodes["A"].Y;
        Assert.True(withTitleY > noTitleY,
            $"Nodes should be shifted down when title is set (no-title Y={noTitleY}, with-title Y={withTitleY})");
    }

    [Fact]
    public void Layout_SequenceDiagram_WithSubtitle_NodesShiftedDown()
    {
        var withoutSubtitle = BuildSequenceDiagram();
        var withSubtitle = BuildSequenceDiagram(subtitle: "Subtitle text");

        _engine.Layout(withoutSubtitle, _theme);
        _engine.Layout(withSubtitle, _theme);

        double noSubtitleY = withoutSubtitle.Nodes["A"].Y;
        double withSubtitleY = withSubtitle.Nodes["A"].Y;
        Assert.True(withSubtitleY > noSubtitleY,
            $"Nodes should be shifted down when subtitle is set (no-subtitle Y={noSubtitleY}, with-subtitle Y={withSubtitleY})");
    }

    [Fact]
    public void Layout_SequenceDiagram_WithTitleAndSubtitle_NodesShiftedMoreThanTitleOnly()
    {
        var withTitle = BuildSequenceDiagram(title: "Title");
        var withBoth = BuildSequenceDiagram(title: "Title", subtitle: "Subtitle");

        _engine.Layout(withTitle, _theme);
        _engine.Layout(withBoth, _theme);

        double titleOnlyY = withTitle.Nodes["A"].Y;
        double bothY = withBoth.Nodes["A"].Y;
        Assert.True(bothY > titleOnlyY,
            $"Nodes should be shifted further down when both title and subtitle are set (title-only Y={titleOnlyY}, both Y={bothY})");
    }

    [Fact]
    public void Layout_SequenceDiagram_WithSubtitle_CanvasHeightIncludesHeadingSpace()
    {
        var withoutSubtitle = BuildSequenceDiagram();
        var withSubtitle = BuildSequenceDiagram(subtitle: "Subtitle text");

        _engine.Layout(withoutSubtitle, _theme);
        _engine.Layout(withSubtitle, _theme);

        double noSubtitleHeight = Convert.ToDouble(withoutSubtitle.Metadata["sequence:canvasHeight"],
            System.Globalization.CultureInfo.InvariantCulture);
        double withSubtitleHeight = Convert.ToDouble(withSubtitle.Metadata["sequence:canvasHeight"],
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(withSubtitleHeight > noSubtitleHeight,
            "Canvas height must increase when subtitle is present");
    }

    // ── BFS (flowchart) heading offset ────────────────────────────────────────

    [Fact]
    public void Layout_Flowchart_WithTitleAndSubtitle_NodesShiftedBelowSubtitleArea()
    {
        // With default theme: titleY ≈ DiagramPadding - 4 = 20, subtitleY ≈ 39.
        // Subtitle text body spans approximately Y=26-39.
        // Nodes must start at Y > subtitle text bottom to avoid overlap.
        var diagram = new Diagram { Title = "Title", Subtitle = "Subtitle" }
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(new Edge("A", "B"));

        _engine.Layout(diagram, _theme);

        double subtitleTextBottom = _theme.DiagramPadding - 4 + _theme.TitleFontSize + 4 + _theme.FontSize;
        foreach (var node in diagram.Nodes.Values)
        {
            Assert.True(node.Y > subtitleTextBottom,
                $"Node Y ({node.Y}) must be below subtitle text bottom (≈{subtitleTextBottom}) to avoid overlap");
        }
    }

    [Fact]
    public void Layout_Flowchart_WithSubtitleOnly_NodesFitBelowSubtitle()
    {
        // Subtitle-only (no title): subtitle renders at DiagramPadding - 4 (same as title position).
        // Subtitle text top ≈ DiagramPadding - 4 - FontSize * 0.85 ≈ 9.  Nodes at DiagramPadding = 24.
        // No overlap expected even without extra offset.
        var diagram = new Diagram { Subtitle = "Context" }
            .AddNode(new Node("A"));

        _engine.Layout(diagram, _theme);

        foreach (var node in diagram.Nodes.Values)
            Assert.True(node.Y > 0, "Node Y must be positive");
    }

    [Fact]
    public void Layout_Flowchart_WithTitleAndSubtitle_NodesLowerThanTitleOnly()
    {
        var titleOnly = new Diagram { Title = "Title" }
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(new Edge("A", "B"));

        var titleAndSubtitle = new Diagram { Title = "Title", Subtitle = "Sub" }
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(new Edge("A", "B"));

        _engine.Layout(titleOnly, _theme);
        _engine.Layout(titleAndSubtitle, _theme);

        double titleOnlyY = titleOnly.Nodes.Values.Min(n => n.Y);
        double bothY = titleAndSubtitle.Nodes.Values.Min(n => n.Y);
        Assert.True(bothY > titleOnlyY,
            $"Nodes should be further down with subtitle (title-only minY={titleOnlyY}, both minY={bothY})");
    }
}

