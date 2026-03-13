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

    [Fact]
    public void Render_DimensionAttributes_UseInvariantCultureDot()
    {
        // Even under a comma-decimal locale the SVG root must use '.' as the decimal separator.
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A")));

        string svg = _renderer.Render(diagram, _theme);

        // width/height must not contain a comma as a decimal separator
        Assert.DoesNotMatch(@"width=""\d+,\d+""", svg);
        Assert.DoesNotMatch(@"height=""\d+,\d+""", svg);
    }

    // ── Direction-aware edge anchors ──────────────────────────────────────────

    [Fact]
    public void Render_HorizontalLayout_EdgePathStartsOnRightSideOfSource()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;
        _layout.Layout(diagram, _theme);

        string svg = _renderer.Render(diagram, _theme);

        // For LR layout A is to the left of B; the path should exist
        Assert.Contains("<path ", svg);
        // Source right-side x = A.X + A.Width = padding + nodeWidth
        double expectedX1 = _theme.DiagramPadding + diagram.Nodes["A"].Width;
        Assert.Contains($"M {F(expectedX1)}", svg);
    }

    [Fact]
    public void Render_VerticalLayout_EdgePathStartsOnBottomOfSource()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;
        _layout.Layout(diagram, _theme);

        string svg = _renderer.Render(diagram, _theme);

        // Source bottom-center y = A.Y + A.Height
        double expectedY1 = diagram.Nodes["A"].Y + diagram.Nodes["A"].Height;
        Assert.Contains(F(expectedY1), svg);
    }

    // ── Attribute escaping ────────────────────────────────────────────────────

    [Fact]
    public void Render_ThemeColorWithQuote_IsEscapedInAttribute()
    {
        // A crafted color that embeds a double-quote to attempt attribute injection
        var theme = new Theme { BackgroundColor = @"#fff"";fill:red;x=""" };
        var diagram = BuildAndLayout(new Diagram().AddNode(new Node("A")));

        string svg = _renderer.Render(diagram, theme);

        // The injected quote must be escaped as &quot; so the attribute value is properly terminated.
        Assert.Contains("&quot;", svg);
        // The raw double-quote must not appear immediately after the BackgroundColor value
        // (i.e., no unescaped attribute break-out like fill="#fff";fill:red)
        Assert.DoesNotContain("fill=\"#fff\";fill:red", svg);
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

    // ── Shape rendering ───────────────────────────────────────────────────────

    [Fact]
    public void Render_CloudShape_ProducesPathElement()
    {
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "Cloud Service") { Shape = Shape.Cloud }));

        string svg = _renderer.Render(diagram, _theme);

        // Cloud is rendered as a <path d="..." />, not a <rect> or <polygon>.
        Assert.Contains("<path d=", svg);
        Assert.Contains("Cloud Service", svg);
    }

    [Fact]
    public void Render_UsesCustomLabelCenterMetadata_WhenPresent()
    {
        var node = new Node("A", "Shifted")
        {
            Shape = Shape.Circle,
            Width = 120,
            Height = 120,
        };
        node.Metadata["label:centerX"] = 30d;
        node.Metadata["label:centerY"] = 40d;

        var diagram = new Diagram().AddNode(node);

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("x=\"30.00\"", svg);
        var expectedY = (40d + _theme.FontSize * 0.35).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains($"y=\"{expectedY}\"", svg);
    }

    [Fact]
    public void Render_TextOnlyNode_RendersOnlyText()
    {
        var textOnly = new Node("overlap", "a+b")
        {
            X = 40,
            Y = 20,
        };
        textOnly.Metadata["render:textOnly"] = true;

        var diagram = new Diagram().AddNode(textOnly);

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains(">a+b</text>", svg);
        Assert.DoesNotContain("<ellipse", svg);
        Assert.DoesNotContain("<rect width=\"0.00\" height=\"0.00\"", svg);
    }

    [Fact]
    public void Render_MultilineLabel_UsesTspans()
    {
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "Urgent\nImportant")));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("<tspan", svg);
        Assert.Contains(">Urgent</tspan>", svg);
        Assert.Contains(">Important</tspan>", svg);
    }

    [Fact]
    public void Render_WrappedLongLabel_UsesTspans()
    {
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "This is a deliberately long label that should wrap instead of staying on a single line")));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("<tspan", svg);
        Assert.True(svg.Split("<tspan", StringSplitOptions.None).Length > 2, "wrapped label should render as multiple tspans");
        Assert.Contains("This is a deliberately", svg);
    }

    [Fact]
    public void Render_PyramidSegmentNode_UsesPolygon()
    {
        var node = new Node("node_0", "Vision")
        {
            Width = 180,
            Height = 60,
        };
        node.Metadata["conceptual:pyramidSegment"] = true;
        node.Metadata["conceptual:pyramidTopWidth"] = 0d;
        node.Metadata["conceptual:pyramidBottomWidth"] = 90d;

        var diagram = new Diagram().AddNode(node);

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("<polygon points=", svg);
        Assert.Contains(">Vision</text>", svg);
    }

    [Fact]
    public void Render_CycleDiagram_UsesCircularArcEdgePaths()
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

        BuildAndLayout(diagram);

        string svg = _renderer.Render(diagram, _theme);

        Assert.Matches(@"<path d=""M [^""]+ A [^""]+"" fill=""none""", svg);
        Assert.DoesNotMatch(@"<path d=""M [^""]+ C [^""]+"" fill=""none""", svg);
    }

    // ── Edge routing ──────────────────────────────────────────────────────────

    [Fact]
    public void Render_OrthogonalRouting_DiagramLevel_ProducesOrthogonalPath()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;
        diagram.LayoutHints.EdgeRouting = EdgeRouting.Orthogonal;
        BuildAndLayout(diagram);

        string svg = _renderer.Render(diagram, _theme);

        // Orthogonal paths use L and A commands, not C (cubic Bézier)
        Assert.Contains("<path d=", svg);
        Assert.DoesNotContain(" C ", svg);
        Assert.Contains(" L ", svg);
    }

    [Fact]
    public void Render_OrthogonalRouting_IncludesArcCommand_ForRoundedCorners()
    {
        // Place A and B with a horizontal offset so the Z-path produces arcs.
        // A is top-left, B is lower-right: produces a horizontal-exit Z-path with bends.
        var nodeA = new Node("A") { X = 24, Y = 24, Width = 120, Height = 40 };
        var nodeB = new Node("B") { X = 220, Y = 120, Width = 120, Height = 40 };
        var diagram = new Diagram();
        diagram.AddNode(nodeA).AddNode(nodeB).AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.EdgeRouting = EdgeRouting.Orthogonal;
        // Skip layout engine — positions are set manually.

        string svg = _renderer.Render(diagram, _theme);

        // Rounded corners use SVG arc command
        Assert.Matches(@"A \d+\.\d+,\d+\.\d+ 0 0 [01]", svg);
    }

    [Fact]
    public void Render_OrthogonalRouting_LR_ProducesOrthogonalPath()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;
        diagram.LayoutHints.EdgeRouting = EdgeRouting.Orthogonal;
        BuildAndLayout(diagram);

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("<path d=", svg);
        Assert.DoesNotContain(" C ", svg);
        Assert.Contains(" L ", svg);
    }

    [Fact]
    public void Render_OrthogonalRouting_PerEdgeOverride_TakesPriorityOverDiagramDefault()
    {
        var diagram = new Diagram();
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;
        diagram.LayoutHints.EdgeRouting = EdgeRouting.Bezier; // diagram default is Bezier
        var orthoEdge = new Edge("A", "B") { Routing = EdgeRouting.Orthogonal };
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(orthoEdge);
        BuildAndLayout(diagram);

        string svg = _renderer.Render(diagram, _theme);

        // The single edge uses Orthogonal despite the Bezier default
        Assert.DoesNotContain(" C ", svg);
        Assert.Contains(" L ", svg);
    }

    [Fact]
    public void Render_StraightRouting_DiagramLevel_ProducesStraightLine()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;
        diagram.LayoutHints.EdgeRouting = EdgeRouting.Straight;
        BuildAndLayout(diagram);

        string svg = _renderer.Render(diagram, _theme);

        // Straight paths use M…L only, no C or A
        Assert.Contains("<path d=", svg);
        Assert.DoesNotContain(" C ", svg);
        Assert.DoesNotContain(" A ", svg);
        Assert.Matches(@"<path d=""M \S+ L \S+"" fill=""none""", svg);
    }

    [Fact]
    public void Render_BezierRouting_DefaultBehavior_UnchangedFromBaseline()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;
        // EdgeRouting.Bezier is the default — no explicit assignment needed
        BuildAndLayout(diagram);

        string svg = _renderer.Render(diagram, _theme);

        Assert.Matches(@"<path d=""M [^""]+ C [^""]+"" fill=""none""", svg);
    }

    [Fact]
    public void Render_OrthogonalRouting_WithLabel_RendersLabelAtMidSegment()
    {
        var diagram = new Diagram();
        diagram.AddNode(new Node("A"))
               .AddNode(new Node("B"))
               .AddEdge(new Edge("A", "B") { Label = new DiagramForge.Models.Label("yes") });
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;
        diagram.LayoutHints.EdgeRouting = EdgeRouting.Orthogonal;
        BuildAndLayout(diagram);

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("yes", svg);
        Assert.Contains("<text ", svg);
    }

    [Fact]
    public void Render_OrthogonalRouting_CustomCornerRadius_UsedInArc()
    {
        // Place nodes with an offset to guarantee Z-path bends with custom radius.
        var nodeA = new Node("A") { X = 24, Y = 24, Width = 120, Height = 40 };
        var nodeB = new Node("B") { X = 220, Y = 120, Width = 120, Height = 40 };
        var diagram = new Diagram();
        diagram.AddNode(nodeA).AddNode(nodeB).AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.EdgeRouting = EdgeRouting.Orthogonal;
        diagram.LayoutHints.OrthogonalCornerRadius = 12.0;
        // Skip layout engine — positions are set manually.

        string svg = _renderer.Render(diagram, _theme);

        // radius 12 should appear in the arc command
        Assert.Contains("A 12.00,12.00", svg);
    }

    [Fact]
    public void Render_OrthogonalRouting_HorizontalBranch_ZeroDeltaX_FallsBackToStraightLine()
    {
        // preferHorizontal=true, x2≈x1 → zero-length outer segments; must degenerate to M…L
        var nodeA = new Node("A") { X = 24, Y = 24, Width = 120, Height = 40 };
        var nodeB = new Node("B") { X = 24, Y = 120, Width = 120, Height = 40 }; // same X, same center
        var diagram = new Diagram();
        diagram.AddNode(nodeA).AddNode(nodeB).AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;
        diagram.LayoutHints.EdgeRouting = EdgeRouting.Orthogonal;

        string svg = _renderer.Render(diagram, _theme);

        // Must not produce zero-length segments — expect a plain M…L straight line
        Assert.DoesNotContain(" A ", svg);
        Assert.Matches(@"<path d=""M \S+ L \S+"" fill=""none""", svg);
    }

    [Fact]
    public void Render_OrthogonalRouting_VerticalBranch_ZeroDeltaY_FallsBackToStraightLine()
    {
        // preferHorizontal=false, y2≈y1 → zero-length outer segments; must degenerate to M…L
        var nodeA = new Node("A") { X = 24, Y = 24, Width = 120, Height = 40 };
        var nodeB = new Node("B") { X = 220, Y = 24, Width = 120, Height = 40 }; // same Y, different X
        var diagram = new Diagram();
        diagram.AddNode(nodeA).AddNode(nodeB).AddEdge(new Edge("A", "B"));
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;
        diagram.LayoutHints.EdgeRouting = EdgeRouting.Orthogonal;

        string svg = _renderer.Render(diagram, _theme);

        // Must not produce zero-length segments — expect a plain M…L straight line
        Assert.DoesNotContain(" A ", svg);
        Assert.Matches(@"<path d=""M \S+ L \S+"" fill=""none""", svg);
    }

    // ── Utilities (mirrors the production SvgRenderer.F() helper) ────────────

    private static string F(double v) =>
        v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}
