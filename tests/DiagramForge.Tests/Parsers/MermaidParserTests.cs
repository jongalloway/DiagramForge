using DiagramForge.Abstractions;
using DiagramForge.Models;
using DiagramForge.Parsers.Mermaid;

namespace DiagramForge.Tests.Parsers;

public class MermaidParserTests
{
    private readonly MermaidParser _parser = new();

    // ── CanParse ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("graph LR\n  A --> B")]
    [InlineData("flowchart TB\n  A --> B")]
    [InlineData("flowchart LR\n  A --> B")]
    [InlineData("graph TD\n  A --> B")]
    [InlineData("block-beta\n  A B C")]
    [InlineData("block\n  A B C")]
    public void CanParse_ReturnsTrue_ForMermaidDiagrams(string diagramText)
    {
        Assert.True(_parser.CanParse(diagramText));
    }

    [Fact]
    public void CanParse_ReturnsTrue_WhenFlowchartHeaderFollowsCommentsAndWhitespace()
    {
        const string text = """

            %% leading comment
            %% another comment
            flowchart LR
              A --> B
            """;

        Assert.True(_parser.CanParse(text));
    }

    [Theory]
    [InlineData("diagram: process\nsteps:\n  - A")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sequenceDiagram\nA->>B: hello")]
    public void CanParse_ReturnsFalse_ForNonMermaidInput(string text)
    {
        Assert.False(_parser.CanParse(text));
    }

    // ── SyntaxId ──────────────────────────────────────────────────────────────

    [Fact]
    public void SyntaxId_IsMermaid()
    {
        Assert.Equal("mermaid", _parser.SyntaxId);
    }

    // ── Parse: nodes ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SimpleEdge_ProducesSourceAndTargetNodes()
    {
        var diagram = _parser.Parse("flowchart LR\n  A --> B");

        Assert.True(diagram.Nodes.ContainsKey("A"));
        Assert.True(diagram.Nodes.ContainsKey("B"));
        Assert.Single(diagram.Edges);
    }

    [Fact]
    public void Parse_NodeWithLabel_ExtractsLabelText()
    {
        var diagram = _parser.Parse("flowchart LR\n  A[Start Here] --> B[End Here]");

        Assert.Equal("Start Here", diagram.Nodes["A"].Label.Text);
        Assert.Equal("End Here", diagram.Nodes["B"].Label.Text);
    }

    [Fact]
    public void Parse_MultipleEdges_ProducesCorrectEdgeCount()
    {
        const string text = """
            flowchart TD
              A --> B
              B --> C
              A --> C
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Edges.Count);
        Assert.Equal(3, diagram.Nodes.Count);
    }

    [Fact]
    public void Parse_EdgeLabel_IsAttachedToEdge()
    {
        var diagram = _parser.Parse("flowchart LR\n  A -->|yes| B");

        Assert.Single(diagram.Edges);
        Assert.Equal("yes", diagram.Edges[0].Label?.Text);
    }

    [Fact]
    public void Parse_DottedEdge_SetsLineStyleToDotted()
    {
        var diagram = _parser.Parse("flowchart LR\n  A -.-> B");

        Assert.Equal(EdgeLineStyle.Dotted, diagram.Edges[0].LineStyle);
    }

    [Fact]
    public void Parse_ThickEdge_SetsLineStyleToThick()
    {
        var diagram = _parser.Parse("flowchart LR\n  A ==> B");

        Assert.Equal(EdgeLineStyle.Thick, diagram.Edges[0].LineStyle);
    }

    [Theory]
    [InlineData("flowchart LR\n  A[Rect] --> B", "A", Shape.Rectangle)]
    [InlineData("flowchart LR\n  A(Round) --> B", "A", Shape.RoundedRectangle)]
    [InlineData("flowchart LR\n  A{Diamond} --> B", "A", Shape.Diamond)]
    [InlineData("flowchart LR\n  A((Circle)) --> B", "A", Shape.Circle)]
    public void Parse_NodeShape_IsSetFromBracketSyntax(string text, string nodeId, Shape expectedShape)
    {
        var diagram = _parser.Parse(text);

        Assert.Equal(expectedShape, diagram.Nodes[nodeId].Shape);
    }

    [Fact]
    public void Parse_LongArrowOperator_PreferredOverShortOperator()
    {
        // "--->": the longer operator must be matched, not the "-->"-prefix
        var diagram = _parser.Parse("flowchart LR\n  A ---> B");

        Assert.Single(diagram.Edges);
        Assert.True(diagram.Nodes.ContainsKey("A"));
        Assert.True(diagram.Nodes.ContainsKey("B"));
    }

    [Fact]
    public void Parse_DoubleArrowOperator_PreferredOverSingleArrow()
    {
        // "-->>": the double-arrow operator must be matched, not just "-->"
        var diagram = _parser.Parse("flowchart LR\n  A -->> B");

        Assert.Single(diagram.Edges);
        Assert.True(diagram.Nodes.ContainsKey("B"));
    }

    // ── Parse: layout direction ───────────────────────────────────────────────

    [Theory]
    [InlineData("flowchart LR\n  A --> B", LayoutDirection.LeftToRight)]
    [InlineData("flowchart RL\n  A --> B", LayoutDirection.RightToLeft)]
    [InlineData("flowchart BT\n  A --> B", LayoutDirection.BottomToTop)]
    [InlineData("flowchart TB\n  A --> B", LayoutDirection.TopToBottom)]
    [InlineData("flowchart TD\n  A --> B", LayoutDirection.TopToBottom)]
    public void Parse_Direction_MapsCorrectly(string text, LayoutDirection expected)
    {
        var diagram = _parser.Parse(text);

        Assert.Equal(expected, diagram.LayoutHints.Direction);
    }

    // ── Parse: metadata ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_SourceSyntax_IsMermaid()
    {
        var diagram = _parser.Parse("flowchart LR\n  A --> B");

        Assert.Equal("mermaid", diagram.SourceSyntax);
    }

    [Fact]
    public void Parse_DiagramType_IsFlowchart()
    {
        var diagram = _parser.Parse("flowchart LR\n  A --> B");

        Assert.Equal("flowchart", diagram.DiagramType);
    }

    [Fact]
    public void Parse_BlockDiagram_SetsDiagramType()
    {
        var diagram = _parser.Parse("block-beta\n  A B C");

        Assert.Equal("block", diagram.DiagramType);
        Assert.Equal("mermaid", diagram.SourceSyntax);
    }

    [Fact]
    public void Parse_BlockDiagram_AssignsGridCoordinatesAndSpan()
    {
        const string text = """
            block-beta
              columns 3
              A B:2
              C
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Nodes.Count);
        Assert.Equal(3, diagram.Metadata["block:columnCount"]);
        Assert.Equal(0, diagram.Nodes["A"].Metadata["block:row"]);
        Assert.Equal(0, diagram.Nodes["A"].Metadata["block:column"]);
        Assert.Equal(1, diagram.Nodes["A"].Metadata["block:span"]);
        Assert.Equal(0, diagram.Nodes["B"].Metadata["block:row"]);
        Assert.Equal(1, diagram.Nodes["B"].Metadata["block:column"]);
        Assert.Equal(2, diagram.Nodes["B"].Metadata["block:span"]);
        Assert.Equal(1, diagram.Nodes["C"].Metadata["block:row"]);
        Assert.Equal(0, diagram.Nodes["C"].Metadata["block:column"]);
    }

    [Fact]
    public void Parse_BlockDiagram_SpaceLeavesColumnGap()
    {
        const string text = """
            block-beta
              columns 3
              A space B
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(0, diagram.Nodes["A"].Metadata["block:column"]);
        Assert.Equal(2, diagram.Nodes["B"].Metadata["block:column"]);
    }

    [Fact]
    public void Parse_BlockDiagram_ArrowBlockCapturesDirection()
    {
        const string text = """
            block-beta
              columns 3
              A go<["Go"]>(right) B
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(Shape.ArrowRight, diagram.Nodes["go"].Shape);
        Assert.Equal("right", diagram.Nodes["go"].Metadata["block:arrowDirection"]);
    }

    [Fact]
    public void Parse_BlockDiagram_EdgeWithLabel_IsAttachedToEdge()
    {
        const string text = """
            block-beta
              columns 2
              A B
              A -- "sync" --> B
            """;

        var diagram = _parser.Parse(text);

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("A", edge.SourceId);
        Assert.Equal("B", edge.TargetId);
        Assert.Equal("sync", edge.Label?.Text);
    }

    [Fact]
    public void Parse_BlockDiagram_PlainEdge_DoesNotCreateOperatorNode()
    {
        const string text = """
            block-beta
              columns 2
              A B
              A --> B
            """;

        var diagram = _parser.Parse(text);

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("A", edge.SourceId);
        Assert.Equal("B", edge.TargetId);
        Assert.DoesNotContain("--", diagram.Nodes.Keys);
    }

    // ── Parse: error cases ────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyText_ThrowsDiagramParseException()
    {
        Assert.Throws<DiagramParseException>(() => _parser.Parse("   "));
    }

    [Fact]
    public void Parse_Comments_AreIgnored()
    {
        const string text = """
            flowchart LR
              %% this is a comment
              A --> B
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Nodes.Count);
        Assert.Single(diagram.Edges);
    }

    [Fact]
    public void Parse_LeadingComments_DispatchesToFlowchartParser()
    {
        // Parser selection is based on the normalized Mermaid document, not the
        // literal first line of raw input. Leading comments must not block dispatch.
        const string text = """
            %% comment before header
            %% another comment
            flowchart LR
              A --> B
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("flowchart", diagram.DiagramType);
        Assert.Equal("mermaid", diagram.SourceSyntax);
        Assert.Single(diagram.Edges);
    }

    [Fact]
    public void Parse_UnsupportedMermaidDiagramType_ThrowsDiagramParseException()
    {
        var ex = Assert.Throws<DiagramParseException>(() =>
            _parser.Parse("sequenceDiagram\n  A->>B: hello"));

        Assert.Contains("unsupported Mermaid diagram type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Parse: subgraphs ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_Subgraph_ProducesGroup()
    {
        const string text = """
            flowchart LR
              subgraph backend
                A --> B
              end
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups);
        Assert.Equal("backend", group.Id);
        Assert.Equal("backend", group.Label.Text);
    }

    [Fact]
    public void Parse_Subgraph_MembersFromBothSidesOfEdge()
    {
        // Mermaid semantics: referencing a node anywhere inside the block makes it
        // a member — so the edge `A --> B` contributes both A and B.
        const string text = """
            flowchart LR
              subgraph G
                A --> B
              end
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups);
        Assert.Contains("A", group.ChildNodeIds);
        Assert.Contains("B", group.ChildNodeIds);
        Assert.Equal(2, group.ChildNodeIds.Count);
    }

    [Fact]
    public void Parse_Subgraph_BracketedTitle_ExtractsIdAndTitle()
    {
        // Canonical explicit-id form: `subgraph ide1 [Backend Services]`
        const string text = """
            flowchart LR
              subgraph ide1 [Backend Services]
                A --> B
              end
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups);
        Assert.Equal("ide1", group.Id);
        Assert.Equal("Backend Services", group.Label.Text);
    }

    [Theory]
    // single token → id *and* title
    [InlineData("subgraph One",               "One",   "One")]
    [InlineData("subgraph a-b-c",             "a-b-c", "a-b-c")]
    // explicit id + bracketed title (space optional, quotes inside brackets stripped)
    [InlineData("subgraph ide1[One]",         "ide1",  "One")]
    [InlineData("subgraph ide1 [One]",        "ide1",  "One")]
    [InlineData("subgraph uid2[\"text\"]",    "uid2",  "text")]
    public void Parse_Subgraph_HeaderVariants_SetIdAndTitle(string header, string expectedId, string expectedTitle)
    {
        var diagram = _parser.Parse($"flowchart LR\n  {header}\n    A\n  end");

        var group = Assert.Single(diagram.Groups);
        Assert.Equal(expectedId, group.Id);
        Assert.Equal(expectedTitle, group.Label.Text);
    }

    [Theory]
    // quoted → title-only, auto-id
    [InlineData("subgraph \"Some Title\"",    "Some Title")]
    // unquoted multi-word → title-only (mermaid drops id when id==title and has whitespace)
    [InlineData("subgraph Some Title",        "Some Title")]
    // bare → no label at all
    [InlineData("subgraph",                   "")]
    public void Parse_Subgraph_TitleOnlyHeaders_AutoAssignId(string header, string expectedTitle)
    {
        var diagram = _parser.Parse($"flowchart LR\n  {header}\n    A\n  end");

        var group = Assert.Single(diagram.Groups);
        Assert.StartsWith("__subgraph", group.Id);
        Assert.Equal(expectedTitle, group.Label.Text);
    }

    [Fact]
    public void Parse_Subgraph_NodeOutsideBlock_NotInGroup()
    {
        const string text = """
            flowchart LR
              subgraph G
                A --> B
              end
              B --> C
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups);
        Assert.DoesNotContain("C", group.ChildNodeIds);
        // and the boundary-crossing edge still parses
        Assert.Contains(diagram.Edges, e => e.SourceId == "B" && e.TargetId == "C");
    }

    [Fact]
    public void Parse_Subgraph_DirectionDirective_IsSkipped()
    {
        // `direction` inside a subgraph is valid Mermaid but out of scope (#14).
        // Must not leak through as a spurious node declaration.
        const string text = """
            flowchart LR
              subgraph G
                direction TB
                A --> B
              end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Nodes.Count);
        Assert.DoesNotContain(diagram.Nodes.Keys, id => id.Contains("direction"));
    }

    [Fact]
    public void Parse_Subgraph_UnterminatedAtEof_IsClosedLeniently()
    {
        // Missing `end`: prefer a best-effort render over throwing.
        const string text = """
            flowchart LR
              subgraph G
                A --> B
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups);
        Assert.Equal(2, group.ChildNodeIds.Count);
    }

    [Fact]
    public void Parse_MultipleSubgraphs_ProduceMultipleGroups()
    {
        const string text = """
            flowchart LR
              subgraph one
                A --> B
              end
              subgraph two
                C --> D
              end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Groups.Count);
        Assert.Equal(new[] { "one", "two" }, diagram.Groups.Select(g => g.Id));
    }

    [Fact]
    public void Parse_NestedSubgraphs_OuterGroupIncludesInnerNodes()
    {
        // Nodes referenced inside an inner subgraph must also be added to the outer
        // subgraph, mirroring Mermaid's flowDb.addSubGraph semantics.
        const string text = """
            flowchart LR
              subgraph outer
                subgraph inner
                  A --> B
                end
              end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Groups.Count);
        var outer = diagram.Groups.Single(g => g.Id == "outer");
        var inner = diagram.Groups.Single(g => g.Id == "inner");

        // Inner group contains the two directly-referenced nodes
        Assert.Contains("A", inner.ChildNodeIds);
        Assert.Contains("B", inner.ChildNodeIds);

        // Outer group must also include them (ancestry propagation)
        Assert.Contains("A", outer.ChildNodeIds);
        Assert.Contains("B", outer.ChildNodeIds);
    }

    // ── Mindmap: CanParse ─────────────────────────────────────────────────────

    [Fact]
    public void CanParse_ReturnsTrue_ForMindmap()
    {
        const string text = """
            mindmap
              root((Product))
                Engineering
            """;

        Assert.True(_parser.CanParse(text));
    }

    // ── Mindmap: Parse ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Mindmap_DiagramTypeIsMindmap()
    {
        const string text = """
            mindmap
              root((Product))
                Engineering
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("mindmap", diagram.DiagramType);
        Assert.Equal("mermaid", diagram.SourceSyntax);
    }

    [Fact]
    public void Parse_Mindmap_RootHasNoParent()
    {
        const string text = """
            mindmap
              root((Product))
                Engineering
            """;

        var diagram = _parser.Parse(text);

        // The root node (node_0) must have no incoming edges.
        var rootNode = diagram.Nodes.Values.First(n => n.Label.Text == "Product");
        Assert.DoesNotContain(diagram.Edges, e => e.TargetId == rootNode.Id);
    }

    [Fact]
    public void Parse_Mindmap_ChildrenLinkedToParent()
    {
        const string text = """
            mindmap
              root((Product))
                Engineering
                Design
            """;

        var diagram = _parser.Parse(text);

        // root → Engineering, root → Design
        Assert.Equal(2, diagram.Edges.Count);
        var rootId = diagram.Nodes.Values.First(n => n.Label.Text == "Product").Id;
        Assert.All(diagram.Edges, e => Assert.Equal(rootId, e.SourceId));
    }

    [Fact]
    public void Parse_Mindmap_GrandchildLinkedToChild()
    {
        const string text = """
            mindmap
              root((Product))
                Engineering
                  Backend
                  Frontend
            """;

        var diagram = _parser.Parse(text);

        var engineeringId = diagram.Nodes.Values.First(n => n.Label.Text == "Engineering").Id;
        var backendId = diagram.Nodes.Values.First(n => n.Label.Text == "Backend").Id;
        var frontendId = diagram.Nodes.Values.First(n => n.Label.Text == "Frontend").Id;

        Assert.Contains(diagram.Edges, e => e.SourceId == engineeringId && e.TargetId == backendId);
        Assert.Contains(diagram.Edges, e => e.SourceId == engineeringId && e.TargetId == frontendId);
    }

    [Fact]
    public void Parse_Mindmap_NodeShapes_AreRecognized()
    {
        const string text = """
            mindmap
              root((Circle))
                square[Square]
                rounded(Rounded)
            """;

        var diagram = _parser.Parse(text);

        var circle = diagram.Nodes.Values.First(n => n.Label.Text == "Circle");
        var square = diagram.Nodes.Values.First(n => n.Label.Text == "Square");
        var rounded = diagram.Nodes.Values.First(n => n.Label.Text == "Rounded");

        Assert.Equal(Shape.Circle, circle.Shape);
        Assert.Equal(Shape.Rectangle, square.Shape);
        Assert.Equal(Shape.RoundedRectangle, rounded.Shape);
    }

    [Fact]
    public void Parse_Mindmap_NodeCount_MatchesTreeSize()
    {
        const string text = """
            mindmap
              root((Product))
                Engineering
                  Backend
                  Frontend
                Design
                Product
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(6, diagram.Nodes.Count);
        Assert.Equal(5, diagram.Edges.Count);
    }

    // ── State diagram: CanParse ───────────────────────────────────────────────

    [Theory]
    [InlineData("stateDiagram-v2\n    [*] --> Idle")]
    [InlineData("stateDiagram\n    [*] --> Idle")]
    public void CanParse_ReturnsTrue_ForStateDiagram(string text)
    {
        Assert.True(_parser.CanParse(text));
    }

    // ── State diagram: Parse ──────────────────────────────────────────────────

    [Fact]
    public void Parse_StateDiagram_DiagramTypeIsStateDiagram()
    {
        var diagram = _parser.Parse("stateDiagram-v2\n    [*] --> Idle");

        Assert.Equal("statediagram", diagram.DiagramType);
        Assert.Equal("mermaid", diagram.SourceSyntax);
    }

    [Fact]
    public void Parse_StateDiagram_SimpleTransition_ProducesNodesAndEdge()
    {
        var diagram = _parser.Parse("stateDiagram-v2\n    Idle --> Running");

        Assert.True(diagram.Nodes.ContainsKey("Idle"));
        Assert.True(diagram.Nodes.ContainsKey("Running"));
        Assert.Single(diagram.Edges);
        Assert.Equal("Idle", diagram.Edges[0].SourceId);
        Assert.Equal("Running", diagram.Edges[0].TargetId);
    }

    [Fact]
    public void Parse_StateDiagram_TransitionLabel_IsAttachedToEdge()
    {
        var diagram = _parser.Parse("stateDiagram-v2\n    Idle --> Running : start");

        Assert.Single(diagram.Edges);
        Assert.Equal("start", diagram.Edges[0].Label?.Text);
    }

    [Fact]
    public void Parse_StateDiagram_StartTerminal_ProducesDistinctStartNode()
    {
        var diagram = _parser.Parse("stateDiagram-v2\n    [*] --> Idle\n    Idle --> [*]");

        // [*] on the left → __start__, [*] on the right → __end__
        Assert.True(diagram.Nodes.ContainsKey("__start__"));
        Assert.True(diagram.Nodes.ContainsKey("__end__"));
    }

    [Fact]
    public void Parse_StateDiagram_StartAndEndTerminals_AreDistinctNodes()
    {
        // [*] on both sides of the diagram must produce two distinct terminal nodes.
        const string text = """
            stateDiagram-v2
                [*] --> Idle
                Idle --> Running : start
                Running --> Idle : stop
                Running --> [*]
            """;

        var diagram = _parser.Parse(text);

        Assert.True(diagram.Nodes.ContainsKey("__start__"));
        Assert.True(diagram.Nodes.ContainsKey("__end__"));
        Assert.False(diagram.Nodes.ContainsKey("[*]"));
    }

    [Fact]
    public void Parse_StateDiagram_TerminalNodes_HaveCircleShape()
    {
        var diagram = _parser.Parse("stateDiagram-v2\n    [*] --> Idle\n    Idle --> [*]");

        Assert.Equal(Shape.Circle, diagram.Nodes["__start__"].Shape);
        Assert.Equal(Shape.Circle, diagram.Nodes["__end__"].Shape);
    }

    [Fact]
    public void Parse_StateDiagram_StateDefinition_SetsLabel()
    {
        const string text = """
            stateDiagram-v2
                s1 : My State
                s1 --> s2
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("My State", diagram.Nodes["s1"].Label.Text);
    }

    [Fact]
    public void Parse_StateDiagram_FullExample_ProducesCorrectNodeAndEdgeCount()
    {
        const string text = """
            stateDiagram-v2
                [*] --> Idle
                Idle --> Running : start
                Running --> Idle : stop
                Running --> [*]
            """;

        var diagram = _parser.Parse(text);

        // __start__, Idle, Running, __end__
        Assert.Equal(4, diagram.Nodes.Count);
        Assert.Equal(4, diagram.Edges.Count);
    }

    [Theory]
    [InlineData("stateDiagram-v2\n    Idle --> Running : start")]   // spaced  " : "
    [InlineData("stateDiagram-v2\n    Idle --> Running: start")]    // tight   ": "
    [InlineData("stateDiagram-v2\n    Idle --> Running :start")]    // leading " :"
    [InlineData("stateDiagram-v2\n    Idle --> Running:start")]     // no spaces ":"
    public void Parse_StateDiagram_TransitionLabel_FlexibleColonStyles(string text)
    {
        var diagram = _parser.Parse(text);

        Assert.Single(diagram.Edges);
        Assert.Equal("start", diagram.Edges[0].Label?.Text);
        Assert.Equal("Running", diagram.Edges[0].TargetId);
    }

    [Fact]
    public void Parse_StateDiagram_StateDefinitionAfterTransition_UpdatesLabel()
    {
        // Node created by transition first (label == id), then definition overrides the label.
        const string text = """
            stateDiagram-v2
                s1 --> s2
                s1 : Initial State
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("Initial State", diagram.Nodes["s1"].Label.Text);
    }
}
