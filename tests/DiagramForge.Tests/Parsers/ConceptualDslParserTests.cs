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
    [InlineData("diagram: venn\nsets:\n  - A")]
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
        var diagram = _parser.Parse("diagram: venn\nsets:\n  - X");

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
        // Venn diagram with no "sets:" section
        Assert.Throws<DiagramParseException>(() =>
            _parser.Parse("diagram: venn\n"));
    }

    [Fact]
    public void Parse_UnknownType_ThrowsDiagramParseException()
    {
        Assert.Throws<DiagramParseException>(() =>
            _parser.Parse("diagram: unknowntype\nitems:\n  - A"));
    }
}
