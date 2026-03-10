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
    public void CanParse_ReturnsTrue_ForMermaidFlowcharts(string text)
    {
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
}
