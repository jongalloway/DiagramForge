using DiagramForge.Layout;
using DiagramForge.Models;

namespace DiagramForge.Tests.Layout;

/// <summary>
/// Unit tests for class-diagram node sizing and layout.
/// Nodes are built by hand (no parser dependency) to isolate the sizing logic.
/// </summary>
public class ClassDiagramLayoutTests
{
    private readonly DefaultLayoutEngine _engine = new();
    private readonly Theme _theme = Theme.Default;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Node ClassNode(string id, string className, params NodeCompartment[] compartments)
    {
        var node = new Node(id, className);
        foreach (var c in compartments)
            node.Compartments.Add(c);
        return node;
    }

    private static NodeCompartment Compartment(params string[] lines) =>
        new NodeCompartment(null, lines.Select(l => new Label(l)));

    // ── Width sizing ──────────────────────────────────────────────────────────

    [Fact]
    public void ClassNode_Width_AtLeastMinNodeWidth()
    {
        var diagram = new Diagram()
            .AddNode(ClassNode("C", "A", Compartment("+x: int")));

        _engine.Layout(diagram, _theme);

        Assert.True(diagram.Nodes["C"].Width >= diagram.LayoutHints.MinNodeWidth);
    }

    [Fact]
    public void ClassNode_Width_DrivenByWidestCompartmentLine()
    {
        // The attribute line is intentionally longer than the class name.
        // The node must be wide enough to accommodate it.
        const string longLine = "+veryLongAttributeName: SomeComplexType";
        var diagram = new Diagram()
            .AddNode(ClassNode("C", "Short", Compartment(longLine)));

        _engine.Layout(diagram, _theme);

        double node = diagram.Nodes["C"].Width;
        double minW = diagram.LayoutHints.MinNodeWidth;

        Assert.True(node > minW,
            $"Width {node} should exceed MinNodeWidth {minW} due to long attribute line");
    }

    [Fact]
    public void ClassNode_Width_LongerCompartmentLineYieldsWiderNode()
    {
        var diagramShort = new Diagram()
            .AddNode(ClassNode("C", "MyClass", Compartment("+x: int")));
        var diagramLong = new Diagram()
            .AddNode(ClassNode("C", "MyClass", Compartment("+veryLongAttributeNameThatIsDefinitelyWider: ComplexType")));

        _engine.Layout(diagramShort, _theme);
        _engine.Layout(diagramLong, _theme);

        Assert.True(diagramLong.Nodes["C"].Width > diagramShort.Nodes["C"].Width,
            "Node with wider compartment line must be wider");
    }

    [Fact]
    public void ClassNode_Width_WideClassNameDominatesOverCompartment()
    {
        // When the class name is longer than any compartment line, the class name drives width.
        const string longName = "AClassNameThatIsDefinitelyWiderThanAnyCompartmentLine";
        var diagram = new Diagram()
            .AddNode(ClassNode("C", longName, Compartment("+x: int")));

        _engine.Layout(diagram, _theme);

        var nodeShort = new Node("C2", "Short");
        nodeShort.Compartments.Add(Compartment("+x: int"));
        var diagramShort = new Diagram().AddNode(nodeShort);
        _engine.Layout(diagramShort, _theme);

        Assert.True(diagram.Nodes["C"].Width > diagramShort.Nodes["C2"].Width,
            "Wider class name must yield wider node");
    }

    [Fact]
    public void ClassNode_Width_AnnotationContributesToWidth()
    {
        var nodeWithAnn = ClassNode("C", "X", Compartment("+x: int"));
        nodeWithAnn.Annotations.Add(new Label("<<ThisIsAVeryLongStereotypeThatExceedsTheClassName>>"));

        var diagramNoAnn = new Diagram()
            .AddNode(ClassNode("C2", "X", Compartment("+x: int")));
        var diagramWithAnn = new Diagram().AddNode(nodeWithAnn);

        _engine.Layout(diagramNoAnn, _theme);
        _engine.Layout(diagramWithAnn, _theme);

        Assert.True(diagramWithAnn.Nodes["C"].Width >= diagramNoAnn.Nodes["C2"].Width,
            "Long annotation must not shrink the node width below the un-annotated version");
    }

    // ── Height sizing ─────────────────────────────────────────────────────────

    [Fact]
    public void ClassNode_Height_AtLeastMinNodeHeight()
    {
        var diagram = new Diagram()
            .AddNode(ClassNode("C", "X", Compartment("+x: int")));

        _engine.Layout(diagram, _theme);

        Assert.True(diagram.Nodes["C"].Height >= diagram.LayoutHints.MinNodeHeight);
    }

    [Fact]
    public void ClassNode_Height_TallerThanEquivalentStandardNode()
    {
        // A class node with a compartment must be taller than a plain node with
        // the same class name, because compartments add height.
        var standard = new Diagram().AddNode(new Node("S", "MyClass"));
        var classNode = new Diagram()
            .AddNode(ClassNode("C", "MyClass", Compartment("+field: int")));

        _engine.Layout(standard, _theme);
        _engine.Layout(classNode, _theme);

        Assert.True(classNode.Nodes["C"].Height > standard.Nodes["S"].Height,
            "Class node with compartment must be taller than equivalent standard node");
    }

    [Fact]
    public void ClassNode_Height_GrowsWithMoreCompartmentLines()
    {
        var fewLines = new Diagram()
            .AddNode(ClassNode("C", "X",
                Compartment("+a: int")));

        var manyLines = new Diagram()
            .AddNode(ClassNode("C", "X",
                Compartment("+a: int", "+b: string", "+c: bool", "+d: double")));

        _engine.Layout(fewLines, _theme);
        _engine.Layout(manyLines, _theme);

        Assert.True(manyLines.Nodes["C"].Height > fewLines.Nodes["C"].Height,
            "More compartment lines must yield greater height");
    }

    [Fact]
    public void ClassNode_Height_GrowsWithMoreCompartments()
    {
        var oneCompartment = new Diagram()
            .AddNode(ClassNode("C", "X",
                Compartment("+a: int")));

        var twoCompartments = new Diagram()
            .AddNode(ClassNode("C", "X",
                Compartment("+a: int"),
                Compartment("+method(): void")));

        _engine.Layout(oneCompartment, _theme);
        _engine.Layout(twoCompartments, _theme);

        Assert.True(twoCompartments.Nodes["C"].Height > oneCompartment.Nodes["C"].Height,
            "More compartments must yield greater height");
    }

    [Fact]
    public void ClassNode_Height_AnnotationIncreasesHeight()
    {
        var withoutAnn = new Diagram()
            .AddNode(ClassNode("C1", "MyClass", Compartment("+x: int")));

        var nodeWithAnn = ClassNode("C2", "MyClass", Compartment("+x: int"));
        nodeWithAnn.Annotations.Add(new Label("<<entity>>"));
        var withAnn = new Diagram().AddNode(nodeWithAnn);

        _engine.Layout(withoutAnn, _theme);
        _engine.Layout(withAnn, _theme);

        Assert.True(withAnn.Nodes["C2"].Height > withoutAnn.Nodes["C1"].Height,
            "Adding a stereotype annotation must increase the header height");
    }

    // ── Label positioning metadata ────────────────────────────────────────────

    [Fact]
    public void ClassNode_LabelCenterY_PlacedInHeaderNotCenter()
    {
        var diagram = new Diagram()
            .AddNode(ClassNode("C", "MyClass",
                Compartment("+a: int", "+b: string"),
                Compartment("+doWork(): void")));

        _engine.Layout(diagram, _theme);

        var node = diagram.Nodes["C"];
        double nodeHalfH = node.Height / 2;

        bool hasCenterY = node.Metadata.TryGetValue("label:centerY", out var cy)
                          && cy is double centerY;

        Assert.True(hasCenterY, "Class node must store label:centerY in Metadata");
        Assert.True((double)node.Metadata["label:centerY"] < nodeHalfH,
            $"label:centerY ({node.Metadata["label:centerY"]}) should be above node center ({nodeHalfH}) so the class name sits in the header");
    }

    [Fact]
    public void ClassNode_HeaderHeight_StoredInMetadata()
    {
        var diagram = new Diagram()
            .AddNode(ClassNode("C", "MyClass", Compartment("+a: int")));

        _engine.Layout(diagram, _theme);

        var node = diagram.Nodes["C"];
        Assert.True(node.Metadata.ContainsKey("class:headerHeight"),
            "Class node must store class:headerHeight in Metadata");
        Assert.True((double)node.Metadata["class:headerHeight"] > 0,
            "class:headerHeight must be positive");
    }

    [Fact]
    public void ClassNode_HeaderHeight_LessThanTotalHeight()
    {
        var diagram = new Diagram()
            .AddNode(ClassNode("C", "MyClass",
                Compartment("+a: int"),
                Compartment("+method(): void")));

        _engine.Layout(diagram, _theme);

        var node = diagram.Nodes["C"];
        double headerH = (double)node.Metadata["class:headerHeight"];

        Assert.True(headerH < node.Height,
            $"Header height {headerH} must be less than total node height {node.Height}");
    }

    // ── Standard (non-class) nodes unchanged ─────────────────────────────────

    [Fact]
    public void StandardNode_NoCompartments_SizesUnchanged()
    {
        // Nodes without compartments must continue to size exactly as before.
        var diagram = new Diagram()
            .AddNode(new Node("A", "Short"))
            .AddNode(new Node("B", "Not Important / Not Urgent"));

        _engine.Layout(diagram, _theme);

        Assert.Equal(diagram.LayoutHints.MinNodeWidth, diagram.Nodes["A"].Width);
        Assert.True(diagram.Nodes["B"].Width > diagram.LayoutHints.MinNodeWidth);
        Assert.False(diagram.Nodes["A"].Metadata.ContainsKey("label:centerY"),
            "Standard nodes must not get label:centerY metadata");
    }

    // ── Directional layout ────────────────────────────────────────────────────

    [Fact]
    public void ClassNodes_DirectionalLayout_LeftToRight_IncreasingX()
    {
        var diagram = new Diagram();
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;

        diagram.AddNode(ClassNode("A", "ClassA",
                Compartment("+fieldA: int"),
                Compartment("+methodA(): void")))
               .AddNode(ClassNode("B", "ClassB",
                Compartment("+fieldB: string")))
               .AddEdge(new Edge("A", "B"));

        _engine.Layout(diagram, _theme);

        Assert.True(diagram.Nodes["A"].X < diagram.Nodes["B"].X,
            "In LR layout A should be left of B");
    }

    [Fact]
    public void ClassNodes_DirectionalLayout_TopToBottom_IncreasingY()
    {
        var diagram = new Diagram();
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;

        diagram.AddNode(ClassNode("A", "Parent",
                Compartment("+id: Guid")))
               .AddNode(ClassNode("B", "Child",
                Compartment("+parentId: Guid"),
                Compartment("+save(): bool")))
               .AddEdge(new Edge("A", "B"));

        _engine.Layout(diagram, _theme);

        Assert.True(diagram.Nodes["A"].Y < diagram.Nodes["B"].Y,
            "In TB layout A should be above B");
    }

    [Fact]
    public void ClassNodes_DirectionalLayout_NodesDoNotOverlap_TopToBottom()
    {
        var diagram = new Diagram();
        diagram.LayoutHints.Direction = LayoutDirection.TopToBottom;

        diagram.AddNode(ClassNode("A", "Alpha",
                Compartment("+x: int", "+y: int"),
                Compartment("+move(): void")))
               .AddNode(ClassNode("B", "Beta",
                Compartment("+name: string")))
               .AddEdge(new Edge("A", "B"));

        _engine.Layout(diagram, _theme);

        var a = diagram.Nodes["A"];
        var b = diagram.Nodes["B"];
        double aBottom = a.Y + a.Height;
        Assert.True(b.Y >= aBottom,
            $"Node B (Y={b.Y}) must start at or below bottom of A (Y={aBottom})");
    }

    [Fact]
    public void ClassNodes_DirectionalLayout_NodesDoNotOverlap_LeftToRight()
    {
        var diagram = new Diagram();
        diagram.LayoutHints.Direction = LayoutDirection.LeftToRight;

        diagram.AddNode(ClassNode("A", "Alpha",
                Compartment("+wideAttribute: SomeLongTypeName")))
               .AddNode(ClassNode("B", "Beta",
                Compartment("+b: int")))
               .AddEdge(new Edge("A", "B"));

        _engine.Layout(diagram, _theme);

        var a = diagram.Nodes["A"];
        var b = diagram.Nodes["B"];
        double aRight = a.X + a.Width;
        Assert.True(b.X >= aRight,
            $"Node B (X={b.X}) must start at or right of right edge of A (X={aRight})");
    }

    // ── Edge anchor validity ──────────────────────────────────────────────────

    [Fact]
    public void ClassNodes_AfterLayout_HavePositivePositionAndSize()
    {
        var diagram = new Diagram();
        diagram.AddNode(ClassNode("A", "Order",
                Compartment("+id: Guid", "+status: string"),
                Compartment("+submit(): void")))
               .AddNode(ClassNode("B", "Customer",
                Compartment("+name: string"),
                Compartment("+getOrders(): Order[]")))
               .AddEdge(new Edge("A", "B"));

        _engine.Layout(diagram, _theme);

        foreach (var (id, node) in diagram.Nodes)
        {
            Assert.True(node.X >= 0, $"{id}.X must be >= 0");
            Assert.True(node.Y >= 0, $"{id}.Y must be >= 0");
            Assert.True(node.Width > 0, $"{id}.Width must be > 0");
            Assert.True(node.Height > 0, $"{id}.Height must be > 0");
        }
    }

    // ── Multi-line compartment line labels ────────────────────────────────────

    [Fact]
    public void ClassNode_MultiLineCompartmentLabel_TallerThanSingleLine()
    {
        // A Line whose Text contains an embedded newline produces two rendered
        // sub-lines. The resulting node must be taller than one with a single-line
        // equivalent so that the sizing and renderer stay in sync.
        var singleLine = new Diagram()
            .AddNode(ClassNode("C", "X", Compartment("+a: int")));

        var multiLineLabel = new Label("+a: int\n+b: string");
        var compartmentWithMultiLine = new NodeCompartment(null, [multiLineLabel]);
        var nodeMulti = new Node("C", "X");
        nodeMulti.Compartments.Add(compartmentWithMultiLine);
        var multiLine = new Diagram().AddNode(nodeMulti);

        _engine.Layout(singleLine, _theme);
        _engine.Layout(multiLine, _theme);

        Assert.True(multiLine.Nodes["C"].Height > singleLine.Nodes["C"].Height,
            "A compartment Line with an embedded newline should produce a taller node than one with a single rendered line");
    }

    [Fact]
    public void ClassNode_MultiLineCompartmentLabel_HeightMatchesTwoSeparateLines()
    {
        // A single Label with an embedded newline should produce the same node height
        // as two separate Label entries with the same text, because both produce the
        // same number of rendered sub-lines.
        var multiLineLabel = new Label("+a: int\n+b: string");
        var compartmentCombined = new NodeCompartment(null, [multiLineLabel]);
        var nodeCombined = new Node("Combined", "X");
        nodeCombined.Compartments.Add(compartmentCombined);
        var diagramCombined = new Diagram().AddNode(nodeCombined);

        var diagramSplit = new Diagram()
            .AddNode(ClassNode("Split", "X",
                new NodeCompartment(null, [new Label("+a: int"), new Label("+b: string")])));

        _engine.Layout(diagramCombined, _theme);
        _engine.Layout(diagramSplit, _theme);

        Assert.Equal(diagramCombined.Nodes["Combined"].Height,
                     diagramSplit.Nodes["Split"].Height);
    }
}
