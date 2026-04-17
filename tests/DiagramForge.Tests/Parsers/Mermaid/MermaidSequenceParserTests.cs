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
        // "-->>" must win over "-->" on the same position
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

    // ── Message index metadata ────────────────────────────────────────────────

    [Fact]
    public void Parse_MessageIndex_StoredOnEdge()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>B: First");

        var edge = Assert.Single(diagram.Edges);
        Assert.True(edge.Metadata.ContainsKey("sequence:messageIndex"));
        Assert.Equal(0, Convert.ToInt32(edge.Metadata["sequence:messageIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_MultipleMessages_IndexesAreSequential()
    {
        const string text = """
            sequenceDiagram
                A->>B: First
                B-->>A: Second
                A->>B: Third
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Edges.Count);
        for (int i = 0; i < diagram.Edges.Count; i++)
        {
            int idx = Convert.ToInt32(diagram.Edges[i].Metadata["sequence:messageIndex"],
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(i, idx);
        }
    }

    // ── Self-messages ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SelfMessage_SourceAndTargetAreTheSameParticipant()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>A: Think");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("A", edge.SourceId);
        Assert.Equal("A", edge.TargetId);
    }

    [Fact]
    public void Parse_SelfMessage_LabelIsPreserved()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>A: Build prompt");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Build prompt", edge.Label?.Text);
    }

    [Fact]
    public void Parse_SelfMessage_MessageIndexIsAssigned()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>A: Think");

        var edge = Assert.Single(diagram.Edges);
        Assert.True(edge.Metadata.ContainsKey("sequence:messageIndex"));
    }

    // ── Title and subtitle directives ─────────────────────────────────────────

    [Fact]
    public void Parse_TitleDirective_SetsDiagramTitle()
    {
        const string text = """
            sequenceDiagram
                title: Login Flow
                A->>B: Hello
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("Login Flow", diagram.Title);
    }

    [Fact]
    public void Parse_TitleDirective_Quoted_SetsDiagramTitle()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    title: \"Quoted Title\"\n    A->>B: msg");

        Assert.Equal("Quoted Title", diagram.Title);
    }

    [Fact]
    public void Parse_SubtitleDirective_SetsDiagramSubtitle()
    {
        const string text = """
            sequenceDiagram
                subtitle: Scenario A
                A->>B: Hello
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("Scenario A", diagram.Subtitle);
    }

    [Fact]
    public void Parse_TitleAndSubtitleDirectives_BothSet()
    {
        const string text = """
            sequenceDiagram
                title: Auth Sequence
                subtitle: Happy path
                participant A as Alice
                A->>B: Login
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("Auth Sequence", diagram.Title);
        Assert.Equal("Happy path", diagram.Subtitle);
    }

    [Fact]
    public void Parse_TitleAndSubtitleDirectives_DoNotCreateParticipants()
    {
        const string text = """
            sequenceDiagram
                title: My Title
                subtitle: My Subtitle
                A->>B: msg
            """;

        var diagram = _parser.Parse(text);

        // Only A and B should be participants; title/subtitle are not nodes
        Assert.Equal(2, diagram.Nodes.Count);
        Assert.True(diagram.Nodes.ContainsKey("A"));
        Assert.True(diagram.Nodes.ContainsKey("B"));
    }

    // ── Autonumber ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Autonumber_SetsDiagramMetadata()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    autonumber\n    A->>B: Hello");

        Assert.True(diagram.Metadata.ContainsKey("sequence:autonumber"));
        Assert.True(diagram.Metadata["sequence:autonumber"] is true);
    }

    [Fact]
    public void Parse_Autonumber_CaseInsensitive()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    AUTONUMBER\n    A->>B: Hello");

        Assert.True(diagram.Metadata.ContainsKey("sequence:autonumber"));
    }

    [Fact]
    public void Parse_Autonumber_EdgeHasAutonumberIndex()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    autonumber\n    A->>B: Hello");

        var edge = Assert.Single(diagram.Edges);
        Assert.True(edge.Metadata.ContainsKey("sequence:autonumberIndex"));
        Assert.Equal(1, Convert.ToInt32(edge.Metadata["sequence:autonumberIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_Autonumber_MultipleMessages_IndexesStartAtOne()
    {
        const string text = """
            sequenceDiagram
                autonumber
                A->>B: First
                B-->>A: Second
                A->>B: Third
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Edges.Count);
        for (int i = 0; i < diagram.Edges.Count; i++)
        {
            int idx = Convert.ToInt32(diagram.Edges[i].Metadata["sequence:autonumberIndex"],
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(i + 1, idx);
        }
    }

    [Fact]
    public void Parse_NoAutonumber_DiagramMetadataKeyAbsent()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>B: Hello");

        Assert.False(diagram.Metadata.ContainsKey("sequence:autonumber"));
    }

    [Fact]
    public void Parse_NoAutonumber_EdgeDoesNotHaveAutonumberIndex()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>B: Hello");

        var edge = Assert.Single(diagram.Edges);
        Assert.False(edge.Metadata.ContainsKey("sequence:autonumberIndex"));
    }

    [Fact]
    public void Parse_Autonumber_MessagesBeforeKeyword_AreNotNumbered()
    {
        const string text = """
            sequenceDiagram
                A->>B: Before
                autonumber
                B-->>A: After
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Edges.Count);
        // Find edge with label "Before" — it should have no autonumber index
        var beforeEdge = diagram.Edges.First(e => e.Label?.Text == "Before");
        Assert.False(beforeEdge.Metadata.ContainsKey("sequence:autonumberIndex"));

        // Find edge with label "After" — it should be numbered 1
        var afterEdge = diagram.Edges.First(e => e.Label?.Text == "After");
        Assert.True(afterEdge.Metadata.ContainsKey("sequence:autonumberIndex"));
        Assert.Equal(1, Convert.ToInt32(afterEdge.Metadata["sequence:autonumberIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    // ── Rect blocks ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_RectRgb_CreatesOneGroup()
    {
        const string text = """
            sequenceDiagram
                rect rgb(255,0,0)
                    A->>B: msg
                end
            """;

        var diagram = _parser.Parse(text);

        Assert.Single(diagram.Groups);
    }

    [Fact]
    public void Parse_RectRgb_GroupFillColorIsPreserved()
    {
        const string text = """
            sequenceDiagram
                rect rgb(0,255,0)
                    A->>B: msg
                end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("rgb(0,255,0)", diagram.Groups[0].FillColor);
    }

    [Fact]
    public void Parse_RectRgba_GroupFillColorIsPreserved()
    {
        const string text = """
            sequenceDiagram
                rect rgba(0,128,255,0.3)
                    A->>B: msg
                end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("rgba(0,128,255,0.3)", diagram.Groups[0].FillColor);
    }

    [Fact]
    public void Parse_RectBlock_GroupHasRectGroupMetadata()
    {
        const string text = """
            sequenceDiagram
                rect rgb(255,0,0)
                    A->>B: msg
                end
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups);
        Assert.True(group.Metadata.TryGetValue("sequence:rectGroup", out var val) && val is true);
    }

    [Fact]
    public void Parse_RectBlock_GroupStartAndEndIndexSet()
    {
        const string text = """
            sequenceDiagram
                A->>B: Before
                rect rgb(255,0,0)
                    A->>B: Inside
                end
                A->>B: After
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups);
        Assert.Equal(1, Convert.ToInt32(group.Metadata["sequence:rectStartIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, Convert.ToInt32(group.Metadata["sequence:rectEndIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_TwoRectBlocks_CreatesTwoGroups()
    {
        const string text = """
            sequenceDiagram
                rect rgb(255,0,0)
                    A->>B: msg1
                end
                rect rgb(0,255,0)
                    A->>B: msg2
                end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Groups.Count);
    }

    [Fact]
    public void Parse_RectWithNoMessages_GroupNotAdded()
    {
        const string text = """
            sequenceDiagram
                rect rgb(255,0,0)
                end
                A->>B: msg
            """;

        var diagram = _parser.Parse(text);

        Assert.Empty(diagram.Groups);
    }

    [Fact]
    public void Parse_RectBlock_DoesNotCreateExtraParticipants()
    {
        const string text = """
            sequenceDiagram
                participant A
                participant B
                rect rgb(255,0,0)
                    A->>B: msg
                end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Nodes.Count);
    }

    [Fact]
    public void Parse_RectBlock_MessagesStillParsed()
    {
        const string text = """
            sequenceDiagram
                rect rgb(255,0,0)
                    A->>B: Hello
                    B-->>A: Hi
                end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Edges.Count);
    }

    // ── Participant icon syntax ───────────────────────────────────────────────

    [Fact]
    public void Parse_ParticipantWithIconBlock_SetsIconRef()
    {
        var diagram = _parser.Parse("""
            sequenceDiagram
                participant A@{ icon: 'heroicons:computer-desktop' }
            """);

        Assert.Equal("heroicons:computer-desktop", diagram.Nodes["A"].IconRef);
    }

    [Fact]
    public void Parse_ParticipantWithIconBlockDoubleQuotes_SetsIconRef()
    {
        var diagram = _parser.Parse("""
            sequenceDiagram
                participant A@{ icon: "heroicons:server" }
            """);

        Assert.Equal("heroicons:server", diagram.Nodes["A"].IconRef);
    }

    [Fact]
    public void Parse_ParticipantWithAliasAndIconBlock_SetsLabelAndIconRef()
    {
        var diagram = _parser.Parse("""
            sequenceDiagram
                participant C as Computer @{ icon: 'heroicons:computer-desktop' }
            """);

        Assert.Equal("Computer", diagram.Nodes["C"].Label.Text);
        Assert.Equal("heroicons:computer-desktop", diagram.Nodes["C"].IconRef);
    }

    [Fact]
    public void Parse_ParticipantWithIconBlock_IdIsCorrect()
    {
        var diagram = _parser.Parse("""
            sequenceDiagram
                participant S@{ icon: 'heroicons:server' }
            """);

        Assert.True(diagram.Nodes.ContainsKey("S"));
        Assert.Equal("S", diagram.Nodes["S"].Label.Text);
    }

    [Fact]
    public void Parse_ParticipantWithoutIconBlock_IconRefIsNull()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    participant A as Alice");

        Assert.Null(diagram.Nodes["A"].IconRef);
    }

    [Fact]
    public void Parse_ParticipantWithMalformedIconBlock_IconRefIsNull()
    {
        // Block has no icon: key
        var diagram = _parser.Parse("""
            sequenceDiagram
                participant A@{ label: 'something' }
            """);

        Assert.Null(diagram.Nodes["A"].IconRef);
    }

    [Fact]
    public void Parse_MultipleParticipantsWithIcons_AllIconRefsSet()
    {
        const string text = """
            sequenceDiagram
                participant C as Client @{ icon: 'heroicons:computer-desktop' }
                participant S as Server @{ icon: 'heroicons:server' }
                C->>S: request
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("heroicons:computer-desktop", diagram.Nodes["C"].IconRef);
        Assert.Equal("heroicons:server", diagram.Nodes["S"].IconRef);
        Assert.Equal("Client", diagram.Nodes["C"].Label.Text);
        Assert.Equal("Server", diagram.Nodes["S"].Label.Text);
    }

    [Fact]
    public void Parse_ParticipantWithIconBlock_ShapeIsRectangle()
    {
        var diagram = _parser.Parse("""
            sequenceDiagram
                participant A@{ icon: 'heroicons:server' }
            """);

        Assert.Equal(Shape.Rectangle, diagram.Nodes["A"].Shape);
    }

    [Fact]
    public void Parse_ParticipantWithIconBlock_SomeParticipantsWithoutIcon()
    {
        const string text = """
            sequenceDiagram
                participant A@{ icon: 'heroicons:server' }
                participant B as Bob
                A->>B: Hello
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal("heroicons:server", diagram.Nodes["A"].IconRef);
        Assert.Null(diagram.Nodes["B"].IconRef);
    }
}
