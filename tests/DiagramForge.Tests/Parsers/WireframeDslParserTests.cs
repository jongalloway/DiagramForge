using DiagramForge.Abstractions;
using DiagramForge.Models;
using DiagramForge.Parsers.Wireframe;

namespace DiagramForge.Tests.Parsers;

public class WireframeDslParserTests
{
    private readonly WireframeDslParser _parser = new();

    // ── SyntaxId ──────────────────────────────────────────────────────────────

    [Fact]
    public void SyntaxId_IsWireframe()
    {
        Assert.Equal("wireframe", _parser.SyntaxId);
    }

    // ── CanParse ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("wireframe")]
    [InlineData("wireframe: Login Screen")]
    [InlineData("WIREFRAME")]
    [InlineData("  wireframe  ")]
    [InlineData("\nwireframe: My App\n")]
    public void CanParse_ReturnsTrue_ForValidFirstLines(string text)
    {
        Assert.True(_parser.CanParse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("diagram: matrix")]
    [InlineData("flowchart LR")]
    [InlineData("# Heading")]
    public void CanParse_ReturnsFalse_ForNonWireframeText(string text)
    {
        Assert.False(_parser.CanParse(text));
    }

    // ── Parse — header ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Header_SetsSourceSyntaxAndDiagramType()
    {
        var diagram = _parser.Parse("wireframe");

        Assert.Equal("wireframe", diagram.SourceSyntax);
        Assert.Equal("wireframe", diagram.DiagramType);
    }

    [Fact]
    public void Parse_Header_WithTitle_SetsTitle()
    {
        var diagram = _parser.Parse("wireframe: Login Screen");

        Assert.Equal("Login Screen", diagram.Title);
    }

    [Fact]
    public void Parse_Header_WithoutTitle_TitleIsNull()
    {
        var diagram = _parser.Parse("wireframe");

        Assert.Null(diagram.Title);
    }

    [Fact]
    public void Parse_EmptyText_ThrowsDiagramParseException()
    {
        Assert.Throws<DiagramParseException>(() => _parser.Parse(""));
    }

    [Fact]
    public void Parse_NonWireframeText_ThrowsDiagramParseException()
    {
        Assert.Throws<DiagramParseException>(() => _parser.Parse("diagram: matrix"));
    }

    // ── Parse — root node ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_AlwaysCreatesRootNode()
    {
        var diagram = _parser.Parse("wireframe");

        Assert.True(diagram.Nodes.ContainsKey(WireframeDslParser.RootNodeId));
        var root = diagram.Nodes[WireframeDslParser.RootNodeId];
        Assert.Equal("column", root.Metadata["wireframe:kind"]);
        Assert.True((bool)root.Metadata["wireframe:isRoot"]);
    }

    // ── Parse — containers ────────────────────────────────────────────────────

    [Theory]
    [InlineData("||| COLUMN |||", "column")]
    [InlineData("||| COLUMN: My Section |||", "column")]
    [InlineData("=== ROW ===", "row")]
    [InlineData("=== ROW: Layout ===", "row")]
    [InlineData("::: CARD :::", "card")]
    [InlineData("::: CARD: Profile :::","card")]
    [InlineData("::: HEADER :::", "header")]
    [InlineData("::: HEADER: Top Bar :::", "header")]
    [InlineData("::: FOOTER :::", "footer")]
    [InlineData("::: FOOTER: Bottom Bar :::", "footer")]
    public void Parse_ContainerStart_SetsCorrectKind(string line, string expectedKind)
    {
        var diagram = _parser.Parse($"wireframe\n{line}\n--- END ---");

        var container = diagram.Nodes.Values
            .FirstOrDefault(n => string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, expectedKind, StringComparison.Ordinal)
                                 && !n.Metadata.ContainsKey("wireframe:isRoot"));

        Assert.NotNull(container);
        Assert.Equal(expectedKind, container.Metadata["wireframe:kind"]);
    }

    [Fact]
    public void Parse_CardWithTitle_SetsLabel()
    {
        var diagram = _parser.Parse("wireframe\n::: CARD: My Card :::\n--- END ---");

        var card = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "card", StringComparison.Ordinal)
            && !n.Metadata.ContainsKey("wireframe:isRoot"));

        Assert.Equal("My Card", card.Label.Text);
    }

    [Fact]
    public void Parse_ContainerEnd_PopsStack()
    {
        // After END, subsequent items belong to the parent container
        const string text = """
            wireframe
            ::: CARD :::
            --- END ---
            [ Submit ](#submit)
            """;

        var diagram = _parser.Parse(text);

        // The button should be a child of root, not of the card
        var button = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "button", StringComparison.Ordinal));

        var card = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "card", StringComparison.Ordinal)
            && !n.Metadata.ContainsKey("wireframe:isRoot"));

        // Button should be child of root, not card
        bool buttonIsChildOfCard = diagram.Edges.Any(e =>
            e.SourceId == card.Id && e.TargetId == button.Id
            && e.Metadata.TryGetValue("wireframe:containment", out var v) && v is true);

        bool buttonIsChildOfRoot = diagram.Edges.Any(e =>
            e.SourceId == WireframeDslParser.RootNodeId && e.TargetId == button.Id
            && e.Metadata.TryGetValue("wireframe:containment", out var v) && v is true);

        Assert.False(buttonIsChildOfCard);
        Assert.True(buttonIsChildOfRoot);
    }

    // ── Parse — buttons ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_Button_SetsKindAndLabel()
    {
        var diagram = _parser.Parse("wireframe\n[ Save ]");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "button", StringComparison.Ordinal));

        Assert.Equal("button", node.Metadata["wireframe:kind"]);
        Assert.Equal("Save", node.Label.Text);
    }

    [Fact]
    public void Parse_Button_WithAction_SetsActionMetadata()
    {
        var diagram = _parser.Parse("wireframe\n[ Login ](#login)");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "button", StringComparison.Ordinal));

        Assert.Equal("login", node.Metadata["wireframe:action"]);
    }

    // ── Parse — text inputs ───────────────────────────────────────────────────

    [Fact]
    public void Parse_TextInput_SetsKindAndPlaceholder()
    {
        var diagram = _parser.Parse("wireframe\n[ text: Email Address ]");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "textinput", StringComparison.Ordinal));

        Assert.Equal("textinput", node.Metadata["wireframe:kind"]);
        Assert.Equal("Email Address", node.Label.Text);
    }

    // ── Parse — checkboxes ────────────────────────────────────────────────────

    [Fact]
    public void Parse_CheckboxUnchecked_SetsCheckedFalse()
    {
        var diagram = _parser.Parse("wireframe\n[ ] Accept Terms");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "checkbox", StringComparison.Ordinal));

        Assert.Equal("checkbox", node.Metadata["wireframe:kind"]);
        Assert.Equal(false, node.Metadata["wireframe:checked"]);
        Assert.Equal("Accept Terms", node.Label.Text);
    }

    [Fact]
    public void Parse_CheckboxChecked_SetsCheckedTrue()
    {
        var diagram = _parser.Parse("wireframe\n[x] Remember Me");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "checkbox", StringComparison.Ordinal));

        Assert.Equal(true, node.Metadata["wireframe:checked"]);
        Assert.Equal("Remember Me", node.Label.Text);
    }

    // ── Parse — radio buttons ─────────────────────────────────────────────────

    [Fact]
    public void Parse_RadioUnchecked_SetsCheckedFalse()
    {
        var diagram = _parser.Parse("wireframe\n( ) Option A");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "radio", StringComparison.Ordinal));

        Assert.Equal("radio", node.Metadata["wireframe:kind"]);
        Assert.Equal(false, node.Metadata["wireframe:checked"]);
    }

    [Fact]
    public void Parse_RadioChecked_SetsCheckedTrue()
    {
        var diagram = _parser.Parse("wireframe\n(x) Option B");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "radio", StringComparison.Ordinal));

        Assert.Equal(true, node.Metadata["wireframe:checked"]);
        Assert.Equal("Option B", node.Label.Text);
    }

    // ── Parse — toggles ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_ToggleOn_SetsOnTrue()
    {
        var diagram = _parser.Parse("wireframe\n[on]");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "toggle", StringComparison.Ordinal));

        Assert.Equal("toggle", node.Metadata["wireframe:kind"]);
        Assert.Equal(true, node.Metadata["wireframe:on"]);
    }

    [Fact]
    public void Parse_ToggleOff_SetsOnFalse()
    {
        var diagram = _parser.Parse("wireframe\n[off]");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "toggle", StringComparison.Ordinal));

        Assert.Equal(false, node.Metadata["wireframe:on"]);
    }

    // ── Parse — dropdowns ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_Dropdown_SetsKindAndValue()
    {
        var diagram = _parser.Parse("wireframe\n[v] Select Country {USA, UK, Canada}");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "dropdown", StringComparison.Ordinal));

        Assert.Equal("dropdown", node.Metadata["wireframe:kind"]);
        Assert.Equal("Select Country", node.Label.Text);
        Assert.Equal("USA, UK, Canada", node.Metadata["wireframe:options"]);
    }

    [Fact]
    public void Parse_Dropdown_WithoutOptions_HasNoOptionsMetadata()
    {
        var diagram = _parser.Parse("wireframe\n[v] Choose");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "dropdown", StringComparison.Ordinal));

        Assert.False(node.Metadata.ContainsKey("wireframe:options"));
    }

    // ── Parse — tabs ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_TabBar_SetsTabsAndActiveTab()
    {
        var diagram = _parser.Parse("wireframe\n|[ Home ]| Profile | Settings |");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "tabs", StringComparison.Ordinal));

        Assert.Equal("tabs", node.Metadata["wireframe:kind"]);
        var tabs = node.Metadata["wireframe:tabs"] as string[];
        Assert.NotNull(tabs);
        Assert.Equal(3, tabs.Length);
        Assert.Equal("Home", tabs[0]);
        Assert.Equal("Profile", tabs[1]);
        Assert.Equal("Settings", tabs[2]);
        Assert.Equal(0, node.Metadata["wireframe:activeTab"]);
    }

    [Fact]
    public void Parse_TabBar_SecondTabActive()
    {
        var diagram = _parser.Parse("wireframe\n| Home |[ Profile ]| Settings |");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "tabs", StringComparison.Ordinal));

        Assert.Equal(1, node.Metadata["wireframe:activeTab"]);
    }

    // ── Parse — badges ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Badge_SetsKindAndLabel()
    {
        var diagram = _parser.Parse("wireframe\n(( New ))");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "badge", StringComparison.Ordinal));

        Assert.Equal("badge", node.Metadata["wireframe:kind"]);
        Assert.Equal("New", node.Label.Text);
    }

    // ── Parse — image placeholder ─────────────────────────────────────────────

    [Fact]
    public void Parse_ImagePlaceholder_SetsKindAndDescription()
    {
        var diagram = _parser.Parse("wireframe\n[ IMG: Profile Photo ]");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "image", StringComparison.Ordinal));

        Assert.Equal("image", node.Metadata["wireframe:kind"]);
        Assert.Equal("Profile Photo", node.Label.Text);
    }

    // ── Parse — headings ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("# Title", 1, "Title")]
    [InlineData("## Subtitle", 2, "Subtitle")]
    [InlineData("### Section", 3, "Section")]
    public void Parse_Heading_SetsLevelAndLabel(string line, int expectedLevel, string expectedText)
    {
        var diagram = _parser.Parse($"wireframe\n{line}");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "heading", StringComparison.Ordinal));

        Assert.Equal("heading", node.Metadata["wireframe:kind"]);
        Assert.Equal(expectedLevel, node.Metadata["wireframe:headingLevel"]);
        Assert.Equal(expectedText, node.Label.Text);
    }

    // ── Parse — divider ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_Divider_SetsKind()
    {
        var diagram = _parser.Parse("wireframe\n***");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "divider", StringComparison.Ordinal));

        Assert.Equal("divider", node.Metadata["wireframe:kind"]);
    }

    // ── Parse — text ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PlainText_SetsKindText()
    {
        var diagram = _parser.Parse("wireframe\nWelcome back!");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "text", StringComparison.Ordinal));

        Assert.Equal("text", node.Metadata["wireframe:kind"]);
        Assert.Equal("Welcome back!", node.Label.Text);
    }

    [Fact]
    public void Parse_BoldText_SetsBoldMetadata()
    {
        var diagram = _parser.Parse("wireframe\n**Important notice**");

        var node = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "text", StringComparison.Ordinal));

        Assert.Equal(true, node.Metadata["wireframe:bold"]);
        Assert.Equal("Important notice", node.Label.Text);
    }

    [Fact]
    public void Parse_LayoutHint_IsSkipped()
    {
        var diagram = _parser.Parse("wireframe\n> align right\n[ OK ]");

        // The '>' hint line should not produce a node; only the button and root exist
        int textNodeCount = diagram.Nodes.Values.Count(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "text", StringComparison.Ordinal));

        Assert.Equal(0, textNodeCount);
    }

    // ── Parse — edges / containment ───────────────────────────────────────────

    [Fact]
    public void Parse_ContainmentEdges_AreCreated()
    {
        var diagram = _parser.Parse("wireframe\n[ text: Username ]");

        // There should be an edge from root to the text input
        var input = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "textinput", StringComparison.Ordinal));

        var edge = diagram.Edges.FirstOrDefault(e =>
            e.SourceId == WireframeDslParser.RootNodeId && e.TargetId == input.Id);

        Assert.NotNull(edge);
        Assert.Equal(true, edge.Metadata["wireframe:containment"]);
    }

    [Fact]
    public void Parse_NestedContainer_ChildrenBelongToContainer()
    {
        const string text = """
            wireframe
            ::: CARD :::
              # Title
            --- END ---
            """;

        var diagram = _parser.Parse(text);

        var card = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "card", StringComparison.Ordinal)
            && !n.Metadata.ContainsKey("wireframe:isRoot"));

        var heading = diagram.Nodes.Values.First(n =>
            string.Equals(n.Metadata.GetValueOrDefault("wireframe:kind") as string, "heading", StringComparison.Ordinal));

        var edge = diagram.Edges.FirstOrDefault(e =>
            e.SourceId == card.Id && e.TargetId == heading.Id
            && e.Metadata.TryGetValue("wireframe:containment", out var v) && v is true);

        Assert.NotNull(edge);
    }

    // ── Parse — layout hints ──────────────────────────────────────────────────

    [Fact]
    public void Parse_SetsLayoutHintsTopToBottom()
    {
        var diagram = _parser.Parse("wireframe");

        Assert.Equal(LayoutDirection.TopToBottom, diagram.LayoutHints.Direction);
    }

    // ── Rendering (smoke test) ────────────────────────────────────────────────

    [Fact]
    public void Render_BasicWireframe_ProducesSvg()
    {
        const string text = """
            wireframe: Login
            ::: HEADER :::
              # Login
            --- END ---
            [ text: Username ]
            [ text: Password ]
            [ Login ](#login)
            """;

        var renderer = new DiagramRenderer();
        var svg = renderer.Render(text);

        Assert.Contains("<svg", svg);
        Assert.Contains("</svg>", svg);
    }

    [Fact]
    public void Render_AllComponentTypes_DoesNotThrow()
    {
        const string text = """
            wireframe: Components
            # Heading 1
            ## Heading 2
            ### Heading 3
            Plain text
            **Bold text**
            ***
            [ Button ]
            [ Submit ](#submit)
            [ text: Placeholder ]
            [ ] Unchecked
            [x] Checked
            ( ) Radio A
            (x) Radio B
            [on]
            [off] Dark mode
            [v] Dropdown {A, B}
            |[ Tab1 ]| Tab2 | Tab3 |
            (( Badge ))
            [ IMG: Photo ]
            """;

        var renderer = new DiagramRenderer();
        var svg = renderer.Render(text);

        Assert.Contains("<svg", svg);
    }
}
