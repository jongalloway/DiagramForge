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
    [InlineData("diagram: matrix\nrows:\n  - R1\ncolumns:\n  - C1")]
    [InlineData("diagram: pyramid\nlevels:\n  - L1")]
    [InlineData("diagram: cycle\nsteps:\n  - S1\n  - S2\n  - S3")]
    public void CanParse_ReturnsTrue_ForKnownTypes(string text)
    {
        Assert.True(_parser.CanParse(text));
    }

    [Theory]
    [InlineData("flowchart LR\n  A --> B")]
    [InlineData("diagram: venn\nsets:\n  - A")]
    [InlineData("diagram: sequenceDiagram")]
    [InlineData("")]
    public void CanParse_ReturnsFalse_ForUnknownInput(string text)
    {
        Assert.False(_parser.CanParse(text));
    }

    // ── Matrix ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Matrix_ProducesFourQuadrantNodes()
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

        Assert.Equal(4, diagram.Nodes.Count);
        Assert.Equal("Col 1\nRow A", diagram.Nodes["cell_0_0"].Label.Text);
        Assert.Equal(0, diagram.Nodes["cell_0_0"].Metadata["matrix:row"]);
        Assert.Equal(0, diagram.Nodes["cell_0_0"].Metadata["matrix:column"]);
        Assert.Equal("Col 2\nRow B", diagram.Nodes["cell_1_1"].Label.Text);
    }

    [Fact]
    public void Parse_Matrix_RequiresExactlyTwoRowsAndTwoColumns()
    {
        const string text = """
            diagram: matrix
            rows:
              - Row A
            columns:
              - Col 1
              - Col 2
            """;

        var ex = Assert.Throws<DiagramParseException>(() => _parser.Parse(text));

        Assert.Contains("exactly 2 rows and 2 columns", ex.Message);
    }

    [Fact]
    public void Parse_Matrix_WithCrLfLineEndings_ProducesFourQuadrantNodes()
    {
        const string text = "diagram: matrix\r\nrows:\r\n  - Row A\r\n  - Row B\r\ncolumns:\r\n  - Col 1\r\n  - Col 2\r\n";

        var diagram = _parser.Parse(text);

        Assert.Equal(4, diagram.Nodes.Count);
        Assert.Equal("Col 1\nRow A", diagram.Nodes["cell_0_0"].Label.Text);
        Assert.Equal("Col 2\nRow B", diagram.Nodes["cell_1_1"].Label.Text);
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
        var diagram = _parser.Parse("diagram: matrix\nrows:\n  - X\n  - Y\ncolumns:\n  - A\n  - B");

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
        Assert.Throws<DiagramParseException>(() =>
            _parser.Parse("diagram: matrix\nrows:\n  - A\n"));
    }

    [Fact]
    public void Parse_UnknownType_ThrowsDiagramParseException()
    {
        Assert.Throws<DiagramParseException>(() =>
            _parser.Parse("diagram: unknowntype\nitems:\n  - A"));
    }

    // ── Cycle ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Cycle_ProducesOneNodePerStep()
    {
        const string text = """
            diagram: cycle
            steps:
              - Plan
              - Build
              - Measure
              - Learn
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(4, diagram.Nodes.Count);
        Assert.Equal("Plan", diagram.Nodes["node_0"].Label.Text);
        Assert.Equal("Learn", diagram.Nodes["node_3"].Label.Text);
    }

    [Fact]
    public void Parse_Cycle_StoresStepIndexMetadata()
    {
        const string text = "diagram: cycle\nsteps:\n  - A\n  - B\n  - C";

        var diagram = _parser.Parse(text);

        Assert.Equal(0, diagram.Nodes["node_0"].Metadata["cycle:stepIndex"]);
        Assert.Equal(1, diagram.Nodes["node_1"].Metadata["cycle:stepIndex"]);
        Assert.Equal(2, diagram.Nodes["node_2"].Metadata["cycle:stepIndex"]);
    }

    [Fact]
    public void Parse_Cycle_CreatesClosedEdgeLoop()
    {
        const string text = "diagram: cycle\nsteps:\n  - A\n  - B\n  - C";

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Edges.Count);
        Assert.Contains(diagram.Edges, e => e.SourceId == "node_0" && e.TargetId == "node_1");
        Assert.Contains(diagram.Edges, e => e.SourceId == "node_1" && e.TargetId == "node_2");
        Assert.Contains(diagram.Edges, e => e.SourceId == "node_2" && e.TargetId == "node_0");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    public void Parse_Cycle_WithInvalidStepCount_ThrowsDiagramParseException(int count)
    {
        var steps = string.Join("\n", Enumerable.Range(0, count).Select(i => $"  - Step{i}"));
        var text = $"diagram: cycle\nsteps:\n{steps}";

        var ex = Assert.Throws<DiagramParseException>(() => _parser.Parse(text));

        Assert.Contains("between 3 and 6 steps", ex.Message);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Parse_Cycle_AcceptsValidStepCounts(int count)
    {
        var steps = string.Join("\n", Enumerable.Range(0, count).Select(i => $"  - Step{i}"));
        var text = $"diagram: cycle\nsteps:\n{steps}";

        var diagram = _parser.Parse(text);

        Assert.Equal(count, diagram.Nodes.Count);
    }

    [Fact]
    public void Parse_Cycle_MissingStepsSection_ThrowsDiagramParseException()
    {
        Assert.Throws<DiagramParseException>(() =>
            _parser.Parse("diagram: cycle\n"));
    }
}
