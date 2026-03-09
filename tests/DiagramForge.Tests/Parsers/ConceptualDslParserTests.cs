using DiagramForge.Abstractions;
using DiagramForge.Models;
using DiagramForge.Parsers.Conceptual;

namespace DiagramForge.Tests.Parsers;

public class ConceptualDslParserTests
{
    private readonly ConceptualDslParser _parser = new();

    // ── SyntaxId ──────────────────────────────────────────────────────────────

    [Fact]
    public void SyntaxId_IsConceptual()
    {
        Assert.Equal("conceptual", _parser.SyntaxId);
    }

    // ── CanParse ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("diagram: process\nsteps:\n  - A")]
    [InlineData("diagram: cycle\nitems:\n  - A")]
    [InlineData("diagram: hierarchy\n  CEO:\n    - CTO")]
    [InlineData("diagram: venn\nsets:\n  - A")]
    [InlineData("diagram: list\nitems:\n  - A")]
    [InlineData("diagram: matrix\nrows:\n  - R1\ncolumns:\n  - C1")]
    [InlineData("diagram: pyramid\nlevels:\n  - L1")]
    public void CanParse_ReturnsTrue_ForKnownTypes(string text)
    {
        Assert.True(_parser.CanParse(text));
    }

    [Theory]
    [InlineData("flowchart LR\n  A --> B")]
    [InlineData("diagram: sequenceDiagram")]
    [InlineData("")]
    public void CanParse_ReturnsFalse_ForUnknownInput(string text)
    {
        Assert.False(_parser.CanParse(text));
    }

    // ── Process ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Process_ProducesChainedEdges()
    {
        const string text = """
            diagram: process
            steps:
              - Discover
              - Plan
              - Build
              - Test
              - Deploy
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(5, diagram.Nodes.Count);
        // 4 edges for a 5-step linear chain
        Assert.Equal(4, diagram.Edges.Count);
        Assert.Equal("process", diagram.DiagramType);
    }

    [Fact]
    public void Parse_Process_NodeLabelsMatchStepNames()
    {
        const string text = "diagram: process\nsteps:\n  - Alpha\n  - Beta";

        var diagram = _parser.Parse(text);

        var labels = diagram.Nodes.Values.Select(n => n.Label.Text).ToList();
        Assert.Contains("Alpha", labels);
        Assert.Contains("Beta", labels);
    }

    // ── Cycle ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Cycle_AddsReturnEdgeFromLastToFirst()
    {
        const string text = """
            diagram: cycle
            items:
              - Plan
              - Build
              - Test
              - Deploy
            """;

        var diagram = _parser.Parse(text);

        // 4 items → 3 forward edges + 1 back edge = 4 total
        Assert.Equal(4, diagram.Edges.Count);
        // Last node ID points back to first
        Assert.Contains(diagram.Edges, e => e.SourceId == "node_3" && e.TargetId == "node_0");
    }

    // ── Hierarchy ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Hierarchy_ProducesParentChildEdges()
    {
        const string text = """
            diagram: hierarchy
            CEO:
              - CTO:
                  - Engineering
              - CFO:
                  - Finance
            """;

        var diagram = _parser.Parse(text);

        Assert.True(diagram.Nodes.Count >= 5, $"Expected ≥5 nodes, got {diagram.Nodes.Count}");
        Assert.True(diagram.Edges.Count >= 4, $"Expected ≥4 edges, got {diagram.Edges.Count}");
        Assert.Equal("hierarchy", diagram.DiagramType);
    }

    // ── Venn ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Venn_ProducesOneNodePerSet()
    {
        const string text = "diagram: venn\nsets:\n  - Engineering\n  - Product\n  - Design";

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Nodes.Count);
    }

    // ── Matrix ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Matrix_ProducesRowsTimesColumnsNodes()
    {
        const string text = """
            diagram: matrix
            rows:
              - Row A
              - Row B
            columns:
              - Col 1
              - Col 2
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(4, diagram.Nodes.Count); // 2×2
    }

    // ── Pyramid ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Pyramid_ProducesOneNodePerLevel()
    {
        const string text = "diagram: pyramid\nlevels:\n  - Vision\n  - Strategy\n  - Tactics";

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Nodes.Count);
    }

    // ── Metadata ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SourceSyntax_IsConceptual()
    {
        var diagram = _parser.Parse("diagram: list\nitems:\n  - X");

        Assert.Equal("conceptual", diagram.SourceSyntax);
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyText_ThrowsDiagramParseException()
    {
        Assert.Throws<DiagramParseException>(() => _parser.Parse("   "));
    }

    [Fact]
    public void Parse_MissingSectionKey_ThrowsDiagramParseException()
    {
        // Process diagram with no "steps:" section
        Assert.Throws<DiagramParseException>(() =>
            _parser.Parse("diagram: process\n"));
    }

    [Fact]
    public void Parse_UnknownType_ThrowsDiagramParseException()
    {
        Assert.Throws<DiagramParseException>(() =>
            _parser.Parse("diagram: unknowntype\nitems:\n  - A"));
    }
}
