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

    // ── Note directives ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("Note right of A: hello", "rightOf", "A", null, "hello")]
    [InlineData("Note left of B: world", "leftOf", "B", null, "world")]
    [InlineData("Note over A: text", "over", "A", null, "text")]
    [InlineData("NOTE RIGHT OF A: caps", "rightOf", "A", null, "caps")]
    public void Parse_Note_SingleParticipant_CreatesGroupWithCorrectMetadata(
        string noteLine, string expectedPos, string expectedP1, string? expectedP2, string expectedText)
    {
        var diagram = _parser.Parse($"sequenceDiagram\n    participant A\n    participant B\n    {noteLine}");

        var noteGroup = Assert.Single(diagram.Groups);
        Assert.True(noteGroup.Metadata.TryGetValue("sequence:noteGroup", out var isNote) && isNote is true);
        Assert.Equal(expectedPos, noteGroup.Metadata["sequence:notePosition"]);
        Assert.Equal(expectedP1, noteGroup.Metadata["sequence:noteParticipant"]);
        Assert.Equal(expectedText, noteGroup.Label.Text);

        if (expectedP2 is null)
            Assert.False(noteGroup.Metadata.ContainsKey("sequence:noteParticipant2"));
        else
            Assert.Equal(expectedP2, noteGroup.Metadata["sequence:noteParticipant2"]);
    }

    [Fact]
    public void Parse_NoteOver_TwoParticipants_StoresBothParticipants()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    Note over A,B: spanning");

        var noteGroup = Assert.Single(diagram.Groups);
        Assert.Equal("over", noteGroup.Metadata["sequence:notePosition"]);
        Assert.Equal("A", noteGroup.Metadata["sequence:noteParticipant"]);
        Assert.Equal("B", noteGroup.Metadata["sequence:noteParticipant2"]);
        Assert.Equal("spanning", noteGroup.Label.Text);
    }

    [Fact]
    public void Parse_Note_AutoCreatesParticipant()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    Note right of Alice: hi");

        Assert.True(diagram.Nodes.ContainsKey("Alice"));
    }

    [Fact]
    public void Parse_Note_SequenceIndexIncremented()
    {
        const string text = """
            sequenceDiagram
                A->>B: msg
                Note right of A: note
                A->>B: after
            """;

        var diagram = _parser.Parse(text);

        // The note occupies sequence index 1; the "after" message occupies index 2.
        var noteGroup = Assert.Single(diagram.Groups);
        Assert.Equal(1, Convert.ToInt32(noteGroup.Metadata["sequence:noteSequenceIndex"],
            System.Globalization.CultureInfo.InvariantCulture));

        var afterEdge = diagram.Edges.Single(e => e.Label?.Text == "after");
        Assert.Equal(2, Convert.ToInt32(afterEdge.Metadata["sequence:messageIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_Note_DoesNotCreateExtraEdges()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>B: msg\n    Note right of A: hi");

        Assert.Single(diagram.Edges);
    }

    [Fact]
    public void Parse_MultilineNote_ContinuationAppended()
    {
        const string text = "sequenceDiagram\n    Note right of A: first line\n        second line";

        var diagram = _parser.Parse(text);

        var noteGroup = Assert.Single(diagram.Groups);
        Assert.Equal("first line\nsecond line", noteGroup.Label.Text);
    }

    [Fact]
    public void Parse_MultilineNote_ContinuationEndsAtNextKeyword()
    {
        const string text = """
            sequenceDiagram
                Note right of A: first
                    continuation
                A->>B: msg
            """;

        var diagram = _parser.Parse(text);

        var noteGroup = Assert.Single(diagram.Groups);
        Assert.Equal("first\ncontinuation", noteGroup.Label.Text);
        Assert.Single(diagram.Edges);
    }

    [Fact]
    public void Parse_MultipleNotes_CreateMultipleGroups()
    {
        const string text = """
            sequenceDiagram
                Note right of A: note1
                Note left of B: note2
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Groups.Count);
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

    [Fact]
    public void Parse_ParticipantWithSubstringKeyName_DoesNotFalselyMatchIcon()
    {
        // "bicon:" contains "icon:" as a substring but must NOT be treated as an icon key.
        var diagram = _parser.Parse("""
            sequenceDiagram
                participant A@{ bicon: 'heroicons:server' }
            """);

        Assert.Null(diagram.Nodes["A"].IconRef);
    }

    [Fact]
    public void Parse_ParticipantWithMultipleMetadataKeys_IconExtractedCorrectly()
    {
        // When icon is not the first key, it should still be found.
        var diagram = _parser.Parse("""
            sequenceDiagram
                participant A@{ shape: 'rect', icon: 'heroicons:server' }
            """);

        Assert.Equal("heroicons:server", diagram.Nodes["A"].IconRef);
    }

    // ── Activate / deactivate (standalone) ───────────────────────────────────

    [Fact]
    public void Parse_Activate_CreatesActivationGroup()
    {
        const string text = """
            sequenceDiagram
                A->>B: call
                activate B
                B-->>A: reply
                deactivate B
            """;

        var diagram = _parser.Parse(text);

        var activation = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:activationGroup", out var v) && v is true);
        Assert.Equal("B", activation.Metadata["sequence:activationParticipant"]);
    }

    [Fact]
    public void Parse_Activate_StartAndEndIndexSet()
    {
        const string text = """
            sequenceDiagram
                A->>B: call
                activate B
                B-->>A: reply
                deactivate B
            """;

        var diagram = _parser.Parse(text);

        var act = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:activationGroup", out var v) && v is true);

        // "activate B" fires after message index 0 (call), so startIndex=1.
        // "deactivate B" fires after message index 1 (reply), so endIndex=1.
        Assert.Equal(1, Convert.ToInt32(act.Metadata["sequence:activationStartIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, Convert.ToInt32(act.Metadata["sequence:activationEndIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_Activate_ActivationGroupMetadataKeySet()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>B: msg\n    activate B\n    B-->>A: reply\n    deactivate B");

        var act = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:activationGroup", out var v) && v is true);
        Assert.True(act.Metadata.TryGetValue("sequence:activationGroup", out var val) && val is true);
    }

    [Fact]
    public void Parse_ActivateWithoutMessages_GroupNotAdded()
    {
        // activate/deactivate with no messages in the range produces no group.
        const string text = """
            sequenceDiagram
                activate B
                deactivate B
                A->>B: msg
            """;

        var diagram = _parser.Parse(text);

        Assert.DoesNotContain(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:activationGroup", out var v) && v is true);
    }

    [Fact]
    public void Parse_NestedActivations_LevelsAssigned()
    {
        const string text = """
            sequenceDiagram
                A->>B: call
                activate B
                B->>B: self
                activate B
                B-->>B: done
                deactivate B
                B-->>A: reply
                deactivate B
            """;

        var diagram = _parser.Parse(text);

        var activations = diagram.Groups
            .Where(g => g.Metadata.TryGetValue("sequence:activationGroup", out var v) && v is true)
            .OrderBy(g => Convert.ToInt32(g.Metadata["sequence:activationLevel"],
                System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        Assert.Equal(2, activations.Count);
        Assert.Equal(0, Convert.ToInt32(activations[0].Metadata["sequence:activationLevel"],
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, Convert.ToInt32(activations[1].Metadata["sequence:activationLevel"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    // ── Activation shorthand (+/-) ────────────────────────────────────────────

    [Fact]
    public void Parse_ActivationShorthandPlus_ActivatesTarget()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>+B: call\n    B-->>A: reply");

        var act = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:activationGroup", out var v) && v is true);
        Assert.Equal("B", act.Metadata["sequence:activationParticipant"]);
    }

    [Fact]
    public void Parse_ActivationShorthandPlus_StripsModifierFromTargetId()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>+B: call\n    B-->>A: reply");

        // Edge target must be "B", not "+B".
        var edge = diagram.Edges.Single(e => e.Label?.Text == "call");
        Assert.Equal("B", edge.TargetId);
        Assert.True(diagram.Nodes.ContainsKey("B"));
    }

    [Fact]
    public void Parse_ActivationShorthandMinus_DeactivatesSource()
    {
        // A->>+B activates B; B-->>-A means deactivate the source (B).
        const string text = """
            sequenceDiagram
                A->>+B: call
                B-->>-A: reply
            """;

        var diagram = _parser.Parse(text);

        var act = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:activationGroup", out var v) && v is true);
        // B was activated; the deactivation ends B's bar.
        Assert.Equal("B", act.Metadata["sequence:activationParticipant"]);
    }

    [Fact]
    public void Parse_ActivationShorthandMinus_StripsModifierFromTargetId()
    {
        var diagram = _parser.Parse("sequenceDiagram\n    A->>+B: call\n    B-->>-A: reply");

        // Edge target in the reply must be "A", not "-A".
        var edge = diagram.Edges.Single(e => e.Label?.Text == "reply");
        Assert.Equal("A", edge.TargetId);
    }

    [Fact]
    public void Parse_ActivationShorthandPlusMinus_ActivationBarSpansMessages()
    {
        const string text = """
            sequenceDiagram
                A->>+B: call
                B-->>-A: reply
            """;

        var diagram = _parser.Parse(text);

        var act = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:activationGroup", out var v) && v is true);

        // startIndex = 0 (call), endIndex = 1 (reply).
        Assert.Equal(0, Convert.ToInt32(act.Metadata["sequence:activationStartIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, Convert.ToInt32(act.Metadata["sequence:activationEndIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    // ── Control-flow blocks (loop / alt / par / critical / break) ─────────────

    [Fact]
    public void Parse_LoopBlock_CreatesGroup()
    {
        const string text = """
            sequenceDiagram
                loop retry
                    A->>B: msg
                end
            """;

        var diagram = _parser.Parse(text);

        Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true);
    }

    [Theory]
    [InlineData("loop", "loop")]
    [InlineData("alt",  "alt")]
    [InlineData("par",  "par")]
    [InlineData("critical", "critical")]
    [InlineData("break", "break")]
    public void Parse_CfBlock_KindMetadataSet(string keyword, string expectedKind)
    {
        var text = $"sequenceDiagram\n    {keyword} condition\n        A->>B: msg\n    end";
        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true);
        Assert.Equal(expectedKind, group.Metadata["sequence:cfKind"]);
    }

    [Fact]
    public void Parse_LoopBlock_LabelStoredAsGroupLabel()
    {
        const string text = """
            sequenceDiagram
                loop i < 10
                    A->>B: msg
                end
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true);
        Assert.Equal("i < 10", group.Label.Text);
    }

    [Fact]
    public void Parse_LoopBlockNoLabel_EmptyGroupLabel()
    {
        const string text = """
            sequenceDiagram
                loop
                    A->>B: msg
                end
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true);
        Assert.Equal(string.Empty, group.Label.Text);
    }

    [Fact]
    public void Parse_CfBlock_StartAndEndIndexSet()
    {
        const string text = """
            sequenceDiagram
                A->>B: before
                loop condition
                    A->>B: inside
                end
                A->>B: after
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true);
        // "inside" is at message index 1.
        Assert.Equal(1, Convert.ToInt32(group.Metadata["sequence:cfStartIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, Convert.ToInt32(group.Metadata["sequence:cfEndIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_CfBlockWithNoMessages_GroupNotAdded()
    {
        const string text = """
            sequenceDiagram
                loop condition
                end
                A->>B: msg
            """;

        var diagram = _parser.Parse(text);

        Assert.DoesNotContain(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true);
    }

    [Fact]
    public void Parse_AltBlock_ElseSeparatorIsIgnored()
    {
        // The "else" keyword is a section separator and must not close the block.
        const string text = """
            sequenceDiagram
                alt user found
                    A->>B: get
                else user not found
                    A->>B: create
                end
            """;

        var diagram = _parser.Parse(text);

        // One CF group that contains both messages.
        var group = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true);
        Assert.Equal(2, diagram.Edges.Count);
        Assert.Equal(0, Convert.ToInt32(group.Metadata["sequence:cfStartIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, Convert.ToInt32(group.Metadata["sequence:cfEndIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_ParBlock_AndSeparatorIsIgnored()
    {
        const string text = """
            sequenceDiagram
                par process 1
                    A->>B: msg1
                and process 2
                    A->>C: msg2
                end
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true);
        Assert.Equal(2, diagram.Edges.Count);
        Assert.Equal(0, Convert.ToInt32(group.Metadata["sequence:cfStartIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, Convert.ToInt32(group.Metadata["sequence:cfEndIndex"],
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_CriticalBlock_OptionSeparatorIsIgnored()
    {
        const string text = """
            sequenceDiagram
                critical Establish a connection
                    A->>B: connect
                option Network timeout
                    A->>B: retry
                end
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true);
        Assert.Equal("critical", group.Metadata["sequence:cfKind"]);
        Assert.Equal(2, diagram.Edges.Count);
    }

    [Fact]
    public void Parse_TwoCfBlocks_CreatesTwoGroups()
    {
        const string text = """
            sequenceDiagram
                loop first
                    A->>B: msg1
                end
                loop second
                    A->>B: msg2
                end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Groups.Count(g =>
            g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true));
    }

    [Fact]
    public void Parse_CfBlock_MessagesStillParsed()
    {
        const string text = """
            sequenceDiagram
                loop cond
                    A->>B: Hello
                    B-->>A: Hi
                end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Edges.Count);
    }

    [Fact]
    public void Parse_CfBlock_DoesNotCreateExtraParticipants()
    {
        const string text = """
            sequenceDiagram
                participant A
                participant B
                loop cond
                    A->>B: msg
                end
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Nodes.Count);
    }

    [Fact]
    public void Parse_BreakBlock_KindIsBreak()
    {
        const string text = """
            sequenceDiagram
                break on exception
                    A->>B: abort
                end
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups,
            g => g.Metadata.TryGetValue("sequence:cfGroup", out var v) && v is true);
        Assert.Equal("break", group.Metadata["sequence:cfKind"]);
    }
}
