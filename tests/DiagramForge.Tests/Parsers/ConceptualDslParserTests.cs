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
}
