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
    [InlineData("diagram: pillars\npillars:\n  - title: A\n  - title: B")]
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

    // ── Pillars ───────────────────────────────────────────────────────────────

    [Fact]
    public void CanParse_ReturnsTrue_ForPillars()
    {
        const string text = "diagram: pillars\npillars:\n  - title: People\n    segments:\n      - Skills\n  - title: Process\n    segments:\n      - Intake";

        Assert.True(_parser.CanParse(text));
    }

    [Fact]
    public void Parse_Pillars_ProducesTitleNodePerPillar()
    {
        const string text = """
            diagram: pillars
            pillars:
              - title: People
                segments:
                  - Skills
                  - Roles
              - title: Process
                segments:
                  - Intake
                  - Delivery
              - title: Technology
                segments:
                  - Platform
                  - Tooling
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Nodes.Count(n => n.Value.Metadata.TryGetValue("pillars:kind", out var k) && "title".Equals(k as string, StringComparison.Ordinal)));
        Assert.True(diagram.Nodes.ContainsKey("pillar_0"));
        Assert.True(diagram.Nodes.ContainsKey("pillar_1"));
        Assert.True(diagram.Nodes.ContainsKey("pillar_2"));
        Assert.Equal("People", diagram.Nodes["pillar_0"].Label.Text);
        Assert.Equal("Process", diagram.Nodes["pillar_1"].Label.Text);
        Assert.Equal("Technology", diagram.Nodes["pillar_2"].Label.Text);
    }

    [Fact]
    public void Parse_Pillars_ProducesSegmentNodesPerPillar()
    {
        const string text = """
            diagram: pillars
            pillars:
              - title: People
                segments:
                  - Skills
                  - Roles
              - title: Process
                segments:
                  - Intake
                  - Delivery
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(6, diagram.Nodes.Count); // 2 titles + 4 segments
        Assert.True(diagram.Nodes.ContainsKey("pillar_0_segment_0"));
        Assert.Equal("Skills", diagram.Nodes["pillar_0_segment_0"].Label.Text);
        Assert.Equal("Roles", diagram.Nodes["pillar_0_segment_1"].Label.Text);
        Assert.Equal("Intake", diagram.Nodes["pillar_1_segment_0"].Label.Text);
        Assert.Equal("Delivery", diagram.Nodes["pillar_1_segment_1"].Label.Text);
    }

    [Fact]
    public void Parse_Pillars_SetsCorrectMetadata()
    {
        const string text = """
            diagram: pillars
            pillars:
              - title: People
                segments:
                  - Skills
              - title: Process
                segments:
                  - Intake
            """;

        var diagram = _parser.Parse(text);

        var titleNode = diagram.Nodes["pillar_0"];
        Assert.Equal(0, titleNode.Metadata["pillars:pillarIndex"]);
        Assert.Equal("title", titleNode.Metadata["pillars:kind"]);

        var segNode = diagram.Nodes["pillar_0_segment_0"];
        Assert.Equal(0, segNode.Metadata["pillars:pillarIndex"]);
        Assert.Equal(0, segNode.Metadata["pillars:segmentIndex"]);
        Assert.Equal("segment", segNode.Metadata["pillars:kind"]);
    }

    [Fact]
    public void Parse_Pillars_WithNoSegments_ProducesOnlyTitleNodes()
    {
        const string text = """
            diagram: pillars
            pillars:
              - title: People
              - title: Process
              - title: Technology
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Nodes.Count);
        Assert.All(diagram.Nodes.Values, n =>
            Assert.Equal("title", n.Metadata["pillars:kind"]));
    }

    [Fact]
    public void Parse_Pillars_WithCrLfLineEndings_ParsesCorrectly()
    {
        const string text = "diagram: pillars\r\npillars:\r\n  - title: People\r\n    segments:\r\n      - Skills\r\n  - title: Process\r\n    segments:\r\n      - Intake\r\n";

        var diagram = _parser.Parse(text);

        Assert.Equal(4, diagram.Nodes.Count);
        Assert.Equal("People", diagram.Nodes["pillar_0"].Label.Text);
        Assert.Equal("Skills", diagram.Nodes["pillar_0_segment_0"].Label.Text);
    }

    [Fact]
    public void Parse_Pillars_TooFewPillars_ThrowsDiagramParseException()
    {
        const string text = """
            diagram: pillars
            pillars:
              - title: People
            """;

        var ex = Assert.Throws<DiagramParseException>(() => _parser.Parse(text));
        Assert.Contains("2 and 5", ex.Message);
    }

    [Fact]
    public void Parse_Pillars_TooManyPillars_ThrowsDiagramParseException()
    {
        const string text = """
            diagram: pillars
            pillars:
              - title: A
              - title: B
              - title: C
              - title: D
              - title: E
              - title: F
            """;

        var ex = Assert.Throws<DiagramParseException>(() => _parser.Parse(text));
        Assert.Contains("2 and 5", ex.Message);
    }

    [Fact]
    public void Parse_Pillars_MissingPillarsSection_ThrowsDiagramParseException()
    {
        const string text = "diagram: pillars\n";

        var ex = Assert.Throws<DiagramParseException>(() => _parser.Parse(text));
        Assert.Contains("pillars:", ex.Message);
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
