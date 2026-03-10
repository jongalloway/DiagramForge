using DiagramForge.Models;
using DiagramForge.Parsers.Mermaid;

namespace DiagramForge.Tests.Parsers.Mermaid;

public class MermaidSequenceParserTests
{
    private readonly MermaidParser _parser = new();

    // ── CanParse ──────────────────────────────────────────────────────────────

    [Fact]
    public void CanParse_ReturnsTrue_ForSequenceDiagram()
    {
        Assert.True(_parser.CanParse("sequenceDiagram\n    A->>B: Hello"));
    }

    [Theory]
    [InlineData("sequenceDiagram")]
    [InlineData("sequenceDiagram\n    participant A")]
    [InlineData("sequenceDiagram\n    A->>B: msg")]
    public void CanParse_ReturnsTrue_ForVariousSequenceInputs(string text)
    {
        Assert.True(_parser.CanParse(text));
    }

    // ── Participant declarations ──────────────────────────────────────────────

    [Fact]
    public void Parse_ParticipantDeclaration_CreatesNodeWithId()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    participant Alice");

        Assert.True(diagram.Nodes.ContainsKey("Alice"));
    }

    [Fact]
    public void Parse_ParticipantWithAlias_UsesAliasAsLabel()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    participant A as Alice");

        Assert.True(diagram.Nodes.ContainsKey("A"));
        Assert.Equal("Alice", diagram.Nodes["A"].Label.Text);
    }

    [Fact]
    public void Parse_ParticipantWithoutAlias_UsesIdAsLabel()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    participant Bob");

        Assert.Equal("Bob", diagram.Nodes["Bob"].Label.Text);
    }

    [Fact]
    public void Parse_MultipleParticipants_AllCreated()
    {
        const string text = """
            sequenceDiagram
                participant A as Alice
                participant B as Bob
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Nodes.Count);
        Assert.True(diagram.Nodes.ContainsKey("A"));
        Assert.True(diagram.Nodes.ContainsKey("B"));
    }

    [Fact]
    public void Parse_ParticipantShape_IsRectangle()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    participant A");

        Assert.Equal(Shape.Rectangle, diagram.Nodes["A"].Shape);
    }

    // ── Message parsing ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_Message_CreatesEdgeBetweenParticipants()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>B: Hello");

        Assert.Single(diagram.Edges);
        Assert.Equal("A", diagram.Edges[0].SourceId);
        Assert.Equal("B", diagram.Edges[0].TargetId);
    }

    [Fact]
    public void Parse_MessageLabel_AttachedToEdge()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>B: Hello World");

        Assert.Equal("Hello World", diagram.Edges[0].Label?.Text);
    }

    [Theory]
    [InlineData("A->>B: msg", EdgeLineStyle.Solid,  ArrowHeadStyle.Arrow)]
    [InlineData("A-->>B: msg", EdgeLineStyle.Dashed, ArrowHeadStyle.Arrow)]
    [InlineData("A->B: msg",   EdgeLineStyle.Solid,  ArrowHeadStyle.None)]
    [InlineData("A-->B: msg",  EdgeLineStyle.Dashed, ArrowHeadStyle.None)]
    public void Parse_ArrowOperator_MapsToCorrectStyles(
        string messageLine, EdgeLineStyle expectedLine, ArrowHeadStyle expectedArrow)
    {
        var diagram = _parser.Parse($"sequenceDiagram\n    {messageLine}");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal(expectedLine, edge.LineStyle);
        Assert.Equal(expectedArrow, edge.ArrowHead);
    }

    [Fact]
    public void Parse_LongerOperatorPreferred_DashedArrow()
    {
        // "-->>‌" must win over "-->‌" on the same position
        var diagram = _parser.Parse("sequenceDiagram\n    A-->>B: hi");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal(EdgeLineStyle.Dashed, edge.LineStyle);
        Assert.Equal(ArrowHeadStyle.Arrow, edge.ArrowHead);
    }

    [Fact]
    public void Parse_MultipleMessages_ProducesCorrectEdgeCount()
    {
        const string text = """
            sequenceDiagram
                participant A as Alice
                participant B as Bob
                A->>B: Hello
                B-->>A: Hi back
                A->>B: Done
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Edges.Count);
    }

    // ── Auto-created participants ─────────────────────────────────────────────

    [Fact]
    public void Parse_UndeclaredParticipants_AutoCreated()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    Alice->>Bob: Hello");

        Assert.True(diagram.Nodes.ContainsKey("Alice"));
        Assert.True(diagram.Nodes.ContainsKey("Bob"));
    }

    [Fact]
    public void Parse_DeclaredThenUsed_ParticipantNotDuplicated()
    {
        const string text = """
            sequenceDiagram
                participant A as Alice
                A->>B: Hello
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Nodes.Count);
    }

    // ── Diagram metadata ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_DiagramType_IsSequenceDiagram()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>B: Hello");

        Assert.Equal("sequencediagram", diagram.DiagramType);
    }

    [Fact]
    public void Parse_SourceSyntax_IsMermaid()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>B: Hello");

        Assert.Equal("mermaid", diagram.SourceSyntax);
    }

    [Fact]
    public void Parse_LayoutDirection_IsLeftToRight()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>B: Hello");

        Assert.Equal(LayoutDirection.LeftToRight, diagram.LayoutHints.Direction);
    }
}
