using DiagramForge.Layout;
using DiagramForge.Models;
using DiagramForge.Rendering;

namespace DiagramForge.Tests.Rendering;

/// <summary>
/// Rendering tests for UML class nodes (compartmented boxes) and UML relationship styles.
/// All diagrams are hand-built; no parser dependency.
/// </summary>
public class SvgClassDiagramRendererTests
{
    private readonly SvgRenderer _renderer = new();
    private readonly DefaultLayoutEngine _layout = new();
    private readonly Theme _theme = Theme.Default;

    private Diagram BuildAndLayout(Diagram diagram)
    {
        _layout.Layout(diagram, _theme);
        return diagram;
    }

    // ── Class node structure ──────────────────────────────────────────────────

    [Fact]
    public void Render_ClassNode_WithCompartments_ProducesOuterRect()
    {
        var node = new Node("Animal", "Animal");
        node.Compartments.Add(new NodeCompartment("attributes", [new Label("-name: String")]));
        node.Compartments.Add(new NodeCompartment("methods", [new Label("+getName(): String")]));

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("<rect ", svg);
        Assert.Contains("rx=\"0\"", svg);
    }

    [Fact]
    public void Render_ClassNode_WithCompartments_ContainsClassName()
    {
        var node = new Node("Animal", "Animal");
        node.Compartments.Add(new NodeCompartment("attributes", [new Label("-name: String")]));

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("Animal", svg);
    }

    [Fact]
    public void Render_ClassNode_WithCompartments_ContainsCompartmentLines()
    {
        var node = new Node("Animal", "Animal");
        node.Compartments.Add(new NodeCompartment("attributes", [new Label("-name: String"), new Label("-age: int")]));
        node.Compartments.Add(new NodeCompartment("methods", [new Label("+getName(): String")]));

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("-name: String", svg);
        Assert.Contains("-age: int", svg);
        Assert.Contains("+getName(): String", svg);
    }

    [Fact]
    public void Render_ClassNode_WithCompartments_ContainsDividerLines()
    {
        var node = new Node("Animal", "Animal");
        node.Compartments.Add(new NodeCompartment("attributes", [new Label("-name: String")]));
        node.Compartments.Add(new NodeCompartment("methods", [new Label("+getName(): String")]));

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        // Each compartment is preceded by a <line> divider, so 2 compartments → 2 dividers
        int lineCount = CountOccurrences(svg, "<line ");
        Assert.Equal(2, lineCount);
    }

    [Fact]
    public void Render_ClassNode_WithCompartments_UsesLeftAlignedText()
    {
        var node = new Node("Animal", "Animal");
        node.Compartments.Add(new NodeCompartment("attributes", [new Label("-name: String")]));

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("text-anchor=\"start\"", svg);
    }

    [Fact]
    public void Render_ClassNode_WithCompartments_ClassNameIsCentered()
    {
        var node = new Node("Animal", "Animal");
        node.Compartments.Add(new NodeCompartment("attributes", [new Label("-name: String")]));

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("text-anchor=\"middle\"", svg);
    }

    [Fact]
    public void Render_ClassNode_WithCompartments_ClassNameIsBold()
    {
        var node = new Node("Animal", "Animal");
        node.Compartments.Add(new NodeCompartment("attributes", [new Label("-name: String")]));

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("font-weight=\"bold\"", svg);
    }

    [Fact]
    public void Render_ClassNode_NoCompartments_FallsBackToStandardRendering()
    {
        var node = new Node("Plain", "Plain Node");

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        // Standard node — no divider lines, no bold class name header
        Assert.DoesNotContain("<line ", svg);
        Assert.DoesNotContain("font-weight=\"bold\"", svg);
        // Standard nodes use rounded rect (rx > 0), not sharp UML corners (rx=0)
        Assert.DoesNotContain("rx=\"0\"", svg);
    }

    // ── Stereotype / annotation rendering ────────────────────────────────────

    [Fact]
    public void Render_ClassNode_WithAnnotation_RendersStereotypeAboveClassName()
    {
        var node = new Node("IAnimal", "IAnimal");
        node.Annotations.Add(new Label("interface"));
        node.Compartments.Add(new NodeCompartment("methods", [new Label("+makeSound(): void")]));

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        // Stereotype wrapped in guillemets
        Assert.Contains("\u00ABinterface\u00BB", svg);
        // Should also contain the class name
        Assert.Contains("IAnimal", svg);
    }

    [Fact]
    public void Render_ClassNode_WithAnnotation_UsesItalicStyle()
    {
        var node = new Node("IAnimal", "IAnimal");
        node.Annotations.Add(new Label("interface"));
        node.Compartments.Add(new NodeCompartment("methods", [new Label("+makeSound(): void")]));

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("font-style=\"italic\"", svg);
    }

    [Fact]
    public void Render_ClassNode_WithAnnotationOnly_NoCompartments_StillRendersBox()
    {
        var node = new Node("Abstract", "AbstractBase");
        node.Annotations.Add(new Label("abstract"));

        var diagram = BuildAndLayout(new Diagram().AddNode(node));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("\u00ABabstract\u00BB", svg);
        Assert.Contains("AbstractBase", svg);
        Assert.Contains("<rect ", svg);
    }

    // ── Node sizing with compartments ─────────────────────────────────────────

    [Fact]
    public void Layout_ClassNode_HeightAccountsForCompartments()
    {
        var node = new Node("Animal", "Animal");
        node.Compartments.Add(new NodeCompartment("attributes",
        [
            new Label("-name: String"),
            new Label("-age: int"),
        ]));
        node.Compartments.Add(new NodeCompartment("methods",
        [
            new Label("+getName(): String"),
            new Label("+makeSound(): void"),
        ]));

        var diagram = new Diagram().AddNode(node);
        _layout.Layout(diagram, _theme);

        // Node with 4 content lines should be taller than theme min node height
        Assert.True(node.Height > _theme.NodePadding * 2 + _theme.FontSize);
    }

    [Fact]
    public void Layout_ClassNode_WidthFitsLongestCompartmentLine()
    {
        var shortNameNode = new Node("A", "A");
        shortNameNode.Compartments.Add(new NodeCompartment("attributes",
        [
            new Label("-veryLongAttributeName: SomeComplexType"),
        ]));

        var diagram = new Diagram().AddNode(shortNameNode);
        _layout.Layout(diagram, _theme);

        // Width should be driven by the long compartment line, not the short class name
        double lineTextWidth = shortNameNode.Width - 2 * _theme.NodePadding;
        Assert.True(lineTextWidth > 0);
        Assert.True(shortNameNode.Width > diagram.LayoutHints.MinNodeWidth);
    }

    // ── UML relationship markers ──────────────────────────────────────────────

    [Fact]
    public void Render_Defs_ContainsAllUmlMarkers()
    {
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(new Edge("A", "B")));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("id=\"arrowhead\"", svg);
        Assert.Contains("id=\"arrowhead-open\"", svg);
        Assert.Contains("id=\"arrowhead-filled-diamond\"", svg);
        Assert.Contains("id=\"arrowhead-open-diamond\"", svg);
    }

    // ── Inheritance (--|>) ────────────────────────────────────────────────────

    [Fact]
    public void Render_InheritanceEdge_UsesOpenArrowMarkerAtTarget()
    {
        var edge = new Edge("Dog", "Animal")
        {
            ArrowHead = ArrowHeadStyle.OpenArrow,
            LineStyle = EdgeLineStyle.Solid,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("Dog"))
            .AddNode(new Node("Animal"))
            .AddEdge(edge));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("marker-end=\"url(#arrowhead-open)\"", svg);
        Assert.DoesNotContain("stroke-dasharray", svg);
    }

    // ── Realization (..|>) ────────────────────────────────────────────────────

    [Fact]
    public void Render_RealizationEdge_UsesDashedLineWithOpenArrow()
    {
        var edge = new Edge("Dog", "IAnimal")
        {
            ArrowHead = ArrowHeadStyle.OpenArrow,
            LineStyle = EdgeLineStyle.Dashed,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("Dog"))
            .AddNode(new Node("IAnimal"))
            .AddEdge(edge));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("marker-end=\"url(#arrowhead-open)\"", svg);
        Assert.Contains("stroke-dasharray", svg);
    }

    // ── Composition (*--) ─────────────────────────────────────────────────────

    [Fact]
    public void Render_CompositionEdge_UsesFilledDiamondAtSource()
    {
        var edge = new Edge("Car", "Engine")
        {
            SourceArrowHead = ArrowHeadStyle.Diamond,
            ArrowHead = ArrowHeadStyle.None,
            LineStyle = EdgeLineStyle.Solid,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("Car"))
            .AddNode(new Node("Engine"))
            .AddEdge(edge));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("marker-start=\"url(#arrowhead-filled-diamond)\"", svg);
        Assert.DoesNotContain("marker-end=\"url(#arrowhead", svg);
    }

    // ── Aggregation (o--) ─────────────────────────────────────────────────────

    [Fact]
    public void Render_AggregationEdge_UsesOpenDiamondAtSource()
    {
        var edge = new Edge("Team", "Player")
        {
            SourceArrowHead = ArrowHeadStyle.Circle,
            ArrowHead = ArrowHeadStyle.None,
            LineStyle = EdgeLineStyle.Solid,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("Team"))
            .AddNode(new Node("Player"))
            .AddEdge(edge));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("marker-start=\"url(#arrowhead-open-diamond)\"", svg);
        Assert.DoesNotContain("marker-end=\"url(#arrowhead", svg);
    }

    // ── Association (-->) ─────────────────────────────────────────────────────

    [Fact]
    public void Render_AssociationEdge_UsesSolidLineWithFilledArrow()
    {
        var edge = new Edge("A", "B")
        {
            ArrowHead = ArrowHeadStyle.Arrow,
            LineStyle = EdgeLineStyle.Solid,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(edge));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("marker-end=\"url(#arrowhead)\"", svg);
        Assert.DoesNotContain("stroke-dasharray", svg);
    }

    // ── Link (--) ─────────────────────────────────────────────────────────────

    [Fact]
    public void Render_LinkEdge_UsesSolidLineWithNoArrowhead()
    {
        var edge = new Edge("A", "B")
        {
            ArrowHead = ArrowHeadStyle.None,
            LineStyle = EdgeLineStyle.Solid,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(edge));

        string svg = _renderer.Render(diagram, _theme);

        Assert.DoesNotContain("marker-end", svg);
        Assert.DoesNotContain("marker-start", svg);
        Assert.DoesNotContain("stroke-dasharray", svg);
    }

    // ── Dependency (..>) ─────────────────────────────────────────────────────

    [Fact]
    public void Render_DependencyEdge_UsesDashedLineWithFilledArrow()
    {
        var edge = new Edge("A", "B")
        {
            ArrowHead = ArrowHeadStyle.Arrow,
            LineStyle = EdgeLineStyle.Dashed,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(edge));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("marker-end=\"url(#arrowhead)\"", svg);
        Assert.Contains("stroke-dasharray", svg);
    }

    // ── Dashed link (..) ─────────────────────────────────────────────────────

    [Fact]
    public void Render_DashedLinkEdge_UsesDashedLineWithNoArrowhead()
    {
        var edge = new Edge("A", "B")
        {
            ArrowHead = ArrowHeadStyle.None,
            LineStyle = EdgeLineStyle.Dashed,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(edge));

        string svg = _renderer.Render(diagram, _theme);

        Assert.DoesNotContain("marker-end", svg);
        Assert.DoesNotContain("marker-start", svg);
        Assert.Contains("stroke-dasharray", svg);
    }

    // ── Relationship labels ───────────────────────────────────────────────────

    [Fact]
    public void Render_EdgeWithLabel_RendersLabelText()
    {
        var edge = new Edge("A", "B")
        {
            Label = new Label("uses"),
            ArrowHead = ArrowHeadStyle.Arrow,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(edge));

        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("uses", svg);
    }

    // ── Existing non-class rendering is unchanged ─────────────────────────────

    [Fact]
    public void Render_StandardNode_WithNoCompartments_UsesStandardShape()
    {
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A", "Hello"))
            .AddNode(new Node("B", "World"))
            .AddEdge(new Edge("A", "B") { ArrowHead = ArrowHeadStyle.Arrow }));

        string svg = _renderer.Render(diagram, _theme);

        Assert.StartsWith("<svg ", svg);
        Assert.Contains("Hello", svg);
        Assert.Contains("World", svg);
        Assert.Contains("marker-end=\"url(#arrowhead)\"", svg);
        // Standard nodes use rounded rect, not sharp corners
        Assert.DoesNotContain("rx=\"0\"", svg);
    }

    [Fact]
    public void Render_SourceArrowHead_DefaultsToNone_IsVerifiedInModelTests()
    {
        // The default value for SourceArrowHead is tested in DiagramModelTests.
        // This test verifies that an edge without source/target arrows renders no marker attributes.
        var edge = new Edge("A", "B")
        {
            ArrowHead = ArrowHeadStyle.None,
        };
        var diagram = BuildAndLayout(new Diagram()
            .AddNode(new Node("A"))
            .AddNode(new Node("B"))
            .AddEdge(edge));

        string svg = _renderer.Render(diagram, _theme);

        Assert.DoesNotContain("marker-end", svg);
        Assert.DoesNotContain("marker-start", svg);
    }

    // ── Full class diagram with multiple relationships ─────────────────────────

    [Fact]
    public void Render_ClassDiagram_WithMultipleRelationshipTypes_ContainsAllMarkers()
    {
        var diagram = new Diagram()
            .AddNode(new Node("Animal", "Animal"))
            .AddNode(new Node("Dog", "Dog"))
            .AddNode(new Node("IFlyable", "IFlyable"))
            .AddNode(new Node("Wing", "Wing"))
            .AddNode(new Node("Flock", "Flock"))
            .AddEdge(new Edge("Dog", "Animal")
            {
                ArrowHead = ArrowHeadStyle.OpenArrow,
                LineStyle = EdgeLineStyle.Solid,
            })
            .AddEdge(new Edge("Dog", "IFlyable")
            {
                ArrowHead = ArrowHeadStyle.OpenArrow,
                LineStyle = EdgeLineStyle.Dashed,
            })
            .AddEdge(new Edge("Dog", "Wing")
            {
                SourceArrowHead = ArrowHeadStyle.Diamond,
                ArrowHead = ArrowHeadStyle.None,
            })
            .AddEdge(new Edge("Flock", "Dog")
            {
                SourceArrowHead = ArrowHeadStyle.Circle,
                ArrowHead = ArrowHeadStyle.None,
            });

        BuildAndLayout(diagram);
        string svg = _renderer.Render(diagram, _theme);

        Assert.Contains("marker-end=\"url(#arrowhead-open)\"", svg);
        Assert.Contains("marker-start=\"url(#arrowhead-filled-diamond)\"", svg);
        Assert.Contains("marker-start=\"url(#arrowhead-open-diamond)\"", svg);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
