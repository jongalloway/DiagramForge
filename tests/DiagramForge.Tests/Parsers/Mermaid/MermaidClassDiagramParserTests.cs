using DiagramForge.Models;
using DiagramForge.Parsers.Mermaid;

namespace DiagramForge.Tests.Parsers.Mermaid;

public class MermaidClassDiagramParserTests
{
    private readonly MermaidParser _parser = new();

    // ── CanParse ──────────────────────────────────────────────────────────────

    [Fact]
    public void CanParse_ReturnsTrue_ForClassDiagram()
    {
        Assert.True(_parser.CanParse("classDiagram\n    class Animal"));
    }

    [Theory]
    [InlineData("classDiagram")]
    [InlineData("classDiagram\n    class Animal")]
    [InlineData("classDiagram\n    Animal <|-- Dog")]
    public void CanParse_ReturnsTrue_ForVariousClassDiagramInputs(string text)
    {
        Assert.True(_parser.CanParse(text));
    }

    [Fact]
    public void CanParse_ReturnsFalse_ForNonMermaidInput()
    {
        Assert.False(_parser.CanParse("not a mermaid diagram"));
    }

    // ── Metadata ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_DiagramType_IsClassDiagram()
    {
        var diagram = _parser.Parse("classDiagram\n    class Animal");

        Assert.Equal("classdiagram", diagram.DiagramType);
    }

    [Fact]
    public void Parse_SourceSyntax_IsMermaid()
    {
        var diagram = _parser.Parse("classDiagram\n    class Animal");

        Assert.Equal("mermaid", diagram.SourceSyntax);
    }

    [Fact]
    public void Parse_DefaultDirection_IsTopToBottom()
    {
        var diagram = _parser.Parse("classDiagram\n    class Animal");

        Assert.Equal(LayoutDirection.TopToBottom, diagram.LayoutHints.Direction);
    }

    // ── Explicit class declarations ───────────────────────────────────────────

    [Fact]
    public void Parse_ExplicitClassDeclaration_CreatesNode()
    {
        var diagram = _parser.Parse("classDiagram\n    class Animal");

        Assert.True(diagram.Nodes.ContainsKey("Animal"));
    }

    [Fact]
    public void Parse_ExplicitClassDeclaration_NodeHasClassShape()
    {
        var diagram = _parser.Parse("classDiagram\n    class Animal");

        Assert.Equal(Shape.Rectangle, diagram.Nodes["Animal"].Shape);
    }

    [Fact]
    public void Parse_ExplicitClassDeclaration_NodeHasClassMetadata()
    {
        var diagram = _parser.Parse("classDiagram\n    class Animal");

        Assert.True(diagram.Nodes["Animal"].Metadata.ContainsKey("class:isClass"));
    }

    [Fact]
    public void Parse_MultipleExplicitClasses_AllCreated()
    {
        const string text = """
            classDiagram
                class Animal
                class Dog
                class Cat
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Nodes.Count);
        Assert.True(diagram.Nodes.ContainsKey("Animal"));
        Assert.True(diagram.Nodes.ContainsKey("Dog"));
        Assert.True(diagram.Nodes.ContainsKey("Cat"));
    }

    // ── Class labels ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ClassWithBracketLabel_SetsLabel()
    {
        var diagram = _parser.Parse("classDiagram\n    class Animal[\"A base Animal\"]");

        Assert.Equal("A base Animal", diagram.Nodes["Animal"].Label.Text);
    }

    [Fact]
    public void Parse_ClassWithoutLabel_UsesIdAsLabel()
    {
        var diagram = _parser.Parse("classDiagram\n    class Animal");

        Assert.Equal("Animal", diagram.Nodes["Animal"].Label.Text);
    }

    // ── Implicit class creation from relationships ────────────────────────────

    [Fact]
    public void Parse_RelationshipWithUndeclaredClasses_ImplicitlyCreatesNodes()
    {
        var diagram = _parser.Parse("classDiagram\n    Animal <|-- Dog");

        Assert.True(diagram.Nodes.ContainsKey("Animal"));
        Assert.True(diagram.Nodes.ContainsKey("Dog"));
    }

    [Fact]
    public void Parse_DeclaredThenUsedInRelationship_NotDuplicated()
    {
        const string text = """
            classDiagram
                class Animal
                Animal <|-- Dog
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Nodes.Count);
    }

    // ── Members via {} block ──────────────────────────────────────────────────

    [Fact]
    public void Parse_ClassBraceBlock_ParsesAttribute()
    {
        const string text = """
            classDiagram
                class Animal {
                    +String name
                }
            """;

        var diagram = _parser.Parse(text);
        var node = diagram.Nodes["Animal"];
        var attrs = node.Compartments.FirstOrDefault(c => c.Kind == "attributes");

        Assert.NotNull(attrs);
        Assert.Single(attrs.Lines);
        Assert.Equal("+String name", attrs.Lines[0].Text);
    }

    [Fact]
    public void Parse_ClassBraceBlock_ParsesMethod()
    {
        const string text = """
            classDiagram
                class Animal {
                    +makeSound() void
                }
            """;

        var diagram = _parser.Parse(text);
        var node = diagram.Nodes["Animal"];
        var methods = node.Compartments.FirstOrDefault(c => c.Kind == "methods");

        Assert.NotNull(methods);
        Assert.Single(methods.Lines);
        Assert.Equal("+makeSound() void", methods.Lines[0].Text);
    }

    [Fact]
    public void Parse_ClassBraceBlock_SeparatesAttributesAndMethods()
    {
        const string text = """
            classDiagram
                class BankAccount {
                    +String owner
                    +BigDecimal balance
                    +deposit(amount) bool
                    +withdrawal(amount) int
                }
            """;

        var diagram = _parser.Parse(text);
        var node = diagram.Nodes["BankAccount"];

        var attrs = node.Compartments.FirstOrDefault(c => c.Kind == "attributes");
        var methods = node.Compartments.FirstOrDefault(c => c.Kind == "methods");

        Assert.NotNull(attrs);
        Assert.NotNull(methods);
        Assert.Equal(2, attrs.Lines.Count);
        Assert.Equal(2, methods.Lines.Count);
    }

    [Fact]
    public void Parse_ClassBraceBlockWithoutClassKeyword_ParsesMembers()
    {
        const string text = """
            classDiagram
                Animal {
                    +String name
                    +makeSound() void
                }
            """;

        var diagram = _parser.Parse(text);
        Assert.True(diagram.Nodes.ContainsKey("Animal"));

        var node = diagram.Nodes["Animal"];
        var attrs = node.Compartments.FirstOrDefault(c => c.Kind == "attributes");
        Assert.NotNull(attrs);
        Assert.Equal("+String name", attrs.Lines[0].Text);
    }

    // ── Members via colon syntax ──────────────────────────────────────────────

    [Fact]
    public void Parse_ColonSyntax_ParsesAttribute()
    {
        var diagram = _parser.Parse("classDiagram\n    Animal : +String name");

        var node = diagram.Nodes["Animal"];
        var attrs = node.Compartments.FirstOrDefault(c => c.Kind == "attributes");

        Assert.NotNull(attrs);
        Assert.Equal("+String name", attrs.Lines[0].Text);
    }

    [Fact]
    public void Parse_ColonSyntax_ParsesMethod()
    {
        var diagram = _parser.Parse("classDiagram\n    Animal : +makeSound() void");

        var node = diagram.Nodes["Animal"];
        var methods = node.Compartments.FirstOrDefault(c => c.Kind == "methods");

        Assert.NotNull(methods);
        Assert.Equal("+makeSound() void", methods.Lines[0].Text);
    }

    [Fact]
    public void Parse_ColonSyntax_MultipleMembers()
    {
        const string text = """
            classDiagram
                Animal : +String name
                Animal : +int age
                Animal : +makeSound() void
            """;

        var diagram = _parser.Parse(text);
        var node = diagram.Nodes["Animal"];

        var attrs = node.Compartments.FirstOrDefault(c => c.Kind == "attributes");
        var methods = node.Compartments.FirstOrDefault(c => c.Kind == "methods");

        Assert.NotNull(attrs);
        Assert.NotNull(methods);
        Assert.Equal(2, attrs.Lines.Count);
        Assert.Single(methods.Lines);
    }

    // ── Visibility markers ────────────────────────────────────────────────────

    [Theory]
    [InlineData("+String name", "attributes")]
    [InlineData("-int age", "attributes")]
    [InlineData("#bool active", "attributes")]
    [InlineData("~Object data", "attributes")]
    [InlineData("+getName() String", "methods")]
    [InlineData("-setAge(int) void", "methods")]
    [InlineData("#isActive() bool", "methods")]
    [InlineData("~packageMethod() void", "methods")]
    public void Parse_VisibilityMarkers_PreservedInMemberText(string member, string expectedKind)
    {
        var text = $"classDiagram\n    Animal : {member}";
        var diagram = _parser.Parse(text);

        var node = diagram.Nodes["Animal"];
        var compartment = node.Compartments.FirstOrDefault(c => c.Kind == expectedKind);

        Assert.NotNull(compartment);
        Assert.Contains(compartment.Lines, l => l.Text == member);
    }

    // ── Attribute vs method distinction ──────────────────────────────────────

    [Fact]
    public void Parse_MemberWithParentheses_IsMethod()
    {
        var diagram = _parser.Parse("classDiagram\n    Animal : +fly() void");

        var node = diagram.Nodes["Animal"];
        Assert.NotNull(node.Compartments.FirstOrDefault(c => c.Kind == "methods"));
        Assert.Null(node.Compartments.FirstOrDefault(c => c.Kind == "attributes"));
    }

    [Fact]
    public void Parse_MemberWithoutParentheses_IsAttribute()
    {
        var diagram = _parser.Parse("classDiagram\n    Animal : +String name");

        var node = diagram.Nodes["Animal"];
        Assert.NotNull(node.Compartments.FirstOrDefault(c => c.Kind == "attributes"));
        Assert.Null(node.Compartments.FirstOrDefault(c => c.Kind == "methods"));
    }

    // ── Direction ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("direction TB", LayoutDirection.TopToBottom)]
    [InlineData("direction TD", LayoutDirection.TopToBottom)]
    [InlineData("direction LR", LayoutDirection.LeftToRight)]
    [InlineData("direction RL", LayoutDirection.RightToLeft)]
    [InlineData("direction BT", LayoutDirection.BottomToTop)]
    public void Parse_DirectionKeyword_SetsLayoutDirection(string dirLine, LayoutDirection expected)
    {
        var text = $"classDiagram\n    {dirLine}\n    class Animal";
        var diagram = _parser.Parse(text);

        Assert.Equal(expected, diagram.LayoutHints.Direction);
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PercentComments_AreIgnored()
    {
        const string text = """
            classDiagram
                %% This is a comment
                class Animal
                %% Another comment
                Animal <|-- Dog
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Nodes.Count);
        Assert.Single(diagram.Edges);
    }

    // ── Relationship operators ────────────────────────────────────────────────

    [Fact]
    public void Parse_Inheritance_ForwardOperator_CreatesEdge()
    {
        // "Animal <|-- Dog": Dog is the child/source, Animal is the parent/target.
        var diagram = _parser.Parse("classDiagram\n    Animal <|-- Dog");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Dog", edge.SourceId);
        Assert.Equal("Animal", edge.TargetId);
        Assert.Equal("inheritance", edge.Metadata["class:relationshipType"]);
        Assert.Equal(EdgeLineStyle.Solid, edge.LineStyle);
        Assert.Equal(ArrowHeadStyle.None, edge.ArrowHead);
    }

    [Fact]
    public void Parse_Inheritance_ReverseOperator_CreatesEdge()
    {
        // "Dog --|> Animal": same relationship as <|--, Dog is still the child/source.
        var diagram = _parser.Parse("classDiagram\n    Dog --|> Animal");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Dog", edge.SourceId);
        Assert.Equal("Animal", edge.TargetId);
        Assert.Equal("inheritance", edge.Metadata["class:relationshipType"]);
    }

    [Fact]
    public void Parse_Composition_ForwardOperator_CreatesEdge()
    {
        var diagram = _parser.Parse("classDiagram\n    Car *-- Engine");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Car", edge.SourceId);
        Assert.Equal("Engine", edge.TargetId);
        Assert.Equal("composition", edge.Metadata["class:relationshipType"]);
        Assert.Equal(EdgeLineStyle.Solid, edge.LineStyle);
        Assert.Equal(ArrowHeadStyle.None, edge.ArrowHead);
    }

    [Fact]
    public void Parse_Composition_ReverseOperator_CreatesEdge()
    {
        // "Engine --* Car": Car is still the whole/source, Engine is the part/target.
        var diagram = _parser.Parse("classDiagram\n    Engine --* Car");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Car", edge.SourceId);
        Assert.Equal("Engine", edge.TargetId);
        Assert.Equal("composition", edge.Metadata["class:relationshipType"]);
    }

    [Fact]
    public void Parse_Aggregation_ForwardOperator_CreatesEdge()
    {
        var diagram = _parser.Parse("classDiagram\n    Zoo o-- Animal");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Zoo", edge.SourceId);
        Assert.Equal("Animal", edge.TargetId);
        Assert.Equal("aggregation", edge.Metadata["class:relationshipType"]);
        Assert.Equal(ArrowHeadStyle.None, edge.ArrowHead);
    }

    [Fact]
    public void Parse_Aggregation_ReverseOperator_CreatesEdge()
    {
        // "Animal --o Zoo": Zoo is still the whole/source, Animal is the aggregated/target.
        var diagram = _parser.Parse("classDiagram\n    Animal --o Zoo");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Zoo", edge.SourceId);
        Assert.Equal("Animal", edge.TargetId);
        Assert.Equal("aggregation", edge.Metadata["class:relationshipType"]);
    }

    [Fact]
    public void Parse_Association_ForwardOperator_CreatesEdge()
    {
        var diagram = _parser.Parse("classDiagram\n    Animal --> Food");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Animal", edge.SourceId);
        Assert.Equal("Food", edge.TargetId);
        Assert.Equal("association", edge.Metadata["class:relationshipType"]);
        Assert.Equal(EdgeLineStyle.Solid, edge.LineStyle);
        Assert.Equal(ArrowHeadStyle.Arrow, edge.ArrowHead);
    }

    [Fact]
    public void Parse_Association_ReverseOperator_CreatesEdge()
    {
        // "Food <-- Animal": same as "Animal --> Food"; Animal is still the source.
        var diagram = _parser.Parse("classDiagram\n    Food <-- Animal");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Animal", edge.SourceId);
        Assert.Equal("Food", edge.TargetId);
        Assert.Equal("association", edge.Metadata["class:relationshipType"]);
    }

    [Fact]
    public void Parse_Link_SolidOperator_CreatesEdge()
    {
        var diagram = _parser.Parse("classDiagram\n    ClassA -- ClassB");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("ClassA", edge.SourceId);
        Assert.Equal("ClassB", edge.TargetId);
        Assert.Equal("link", edge.Metadata["class:relationshipType"]);
        Assert.Equal(EdgeLineStyle.Solid, edge.LineStyle);
        Assert.Equal(ArrowHeadStyle.None, edge.ArrowHead);
    }

    [Fact]
    public void Parse_Dependency_ForwardOperator_CreatesEdge()
    {
        var diagram = _parser.Parse("classDiagram\n    Client ..> Service");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Client", edge.SourceId);
        Assert.Equal("Service", edge.TargetId);
        Assert.Equal("dependency", edge.Metadata["class:relationshipType"]);
        Assert.Equal(EdgeLineStyle.Dashed, edge.LineStyle);
        Assert.Equal(ArrowHeadStyle.Arrow, edge.ArrowHead);
    }

    [Fact]
    public void Parse_Dependency_ReverseOperator_CreatesEdge()
    {
        // "Service <.. Client": same as "Client ..> Service"; Client is still the dependent/source.
        var diagram = _parser.Parse("classDiagram\n    Service <.. Client");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Client", edge.SourceId);
        Assert.Equal("Service", edge.TargetId);
        Assert.Equal("dependency", edge.Metadata["class:relationshipType"]);
        Assert.Equal(EdgeLineStyle.Dashed, edge.LineStyle);
    }

    [Fact]
    public void Parse_Realization_ForwardOperator_CreatesEdge()
    {
        // "IFlyable <|.. Bird": Bird is the implementer/source, IFlyable is the interface/target.
        var diagram = _parser.Parse("classDiagram\n    IFlyable <|.. Bird");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Bird", edge.SourceId);
        Assert.Equal("IFlyable", edge.TargetId);
        Assert.Equal("realization", edge.Metadata["class:relationshipType"]);
        Assert.Equal(EdgeLineStyle.Dashed, edge.LineStyle);
        Assert.Equal(ArrowHeadStyle.None, edge.ArrowHead);
    }

    [Fact]
    public void Parse_Realization_ReverseOperator_CreatesEdge()
    {
        // "Bird ..|> IFlyable": same relationship; Bird is still the implementer/source.
        var diagram = _parser.Parse("classDiagram\n    Bird ..|> IFlyable");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("Bird", edge.SourceId);
        Assert.Equal("IFlyable", edge.TargetId);
        Assert.Equal("realization", edge.Metadata["class:relationshipType"]);
    }

    [Fact]
    public void Parse_DashedLink_Operator_CreatesEdge()
    {
        var diagram = _parser.Parse("classDiagram\n    ClassA .. ClassB");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("ClassA", edge.SourceId);
        Assert.Equal("ClassB", edge.TargetId);
        Assert.Equal("link", edge.Metadata["class:relationshipType"]);
        Assert.Equal(EdgeLineStyle.Dashed, edge.LineStyle);
    }

    // ── Relationship labels ───────────────────────────────────────────────────

    [Fact]
    public void Parse_RelationshipWithLabel_AttachedToEdge()
    {
        var diagram = _parser.Parse("classDiagram\n    Animal --> Food : eats");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("eats", edge.Label?.Text);
    }

    [Fact]
    public void Parse_RelationshipWithoutLabel_EdgeLabelIsNull()
    {
        var diagram = _parser.Parse("classDiagram\n    Animal --> Food");

        var edge = Assert.Single(diagram.Edges);
        Assert.Null(edge.Label);
    }

    [Fact]
    public void Parse_RelationshipLabelWithSpaces_PreservedInLabel()
    {
        var diagram = _parser.Parse("classDiagram\n    Student --> Course : enrolled in");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("enrolled in", edge.Label?.Text);
    }

    // ── Complex diagrams ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_FullClassDiagram_ProducesCorrectSemanticModel()
    {
        const string text = """
            classDiagram
                %% Animal hierarchy
                class Animal {
                    +String name
                    +int age
                    +makeSound() void
                }
                class Dog {
                    +fetch() void
                }
                class Cat {
                    +purr() void
                }
                Animal <|-- Dog
                Animal <|-- Cat
                Animal --> Food : eats
            """;

        var diagram = _parser.Parse(text);

        // Nodes
        Assert.Equal(4, diagram.Nodes.Count); // Animal, Dog, Cat, Food
        Assert.True(diagram.Nodes.ContainsKey("Animal"));
        Assert.True(diagram.Nodes.ContainsKey("Dog"));
        Assert.True(diagram.Nodes.ContainsKey("Cat"));
        Assert.True(diagram.Nodes.ContainsKey("Food"));

        // Animal compartments
        var animal = diagram.Nodes["Animal"];
        var attrs = animal.Compartments.First(c => c.Kind == "attributes");
        var methods = animal.Compartments.First(c => c.Kind == "methods");
        Assert.Equal(2, attrs.Lines.Count);
        Assert.Single(methods.Lines);

        // Edges
        Assert.Equal(3, diagram.Edges.Count);
        Assert.Equal(2, diagram.Edges.Count(e => (string)e.Metadata["class:relationshipType"] == "inheritance"));
        Assert.Single(diagram.Edges, e => (string)e.Metadata["class:relationshipType"] == "association");
        Assert.Equal("eats", diagram.Edges.First(e => (string)e.Metadata["class:relationshipType"] == "association").Label?.Text);
    }

    [Fact]
    public void Parse_MultipleRelationships_AllEdgesCreated()
    {
        const string text = """
            classDiagram
                Vehicle <|-- Car
                Vehicle <|-- Truck
                Car *-- Engine
                Car o-- Wheel
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(4, diagram.Edges.Count);
    }

    // ── Operator reversal metadata ────────────────────────────────────────────

    [Fact]
    public void Parse_LeftArrowInheritanceOp_OperatorReversedIsTrue()
    {
        // <|-- has the arrow on the left; isReversed=true causes SourceId/TargetId swap.
        var diagram = _parser.Parse("classDiagram\n    Animal <|-- Dog");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal(true, edge.Metadata["class:operatorReversed"]);
    }

    [Fact]
    public void Parse_RightArrowInheritanceOp_OperatorReversedIsFalse()
    {
        // --|> has the arrow on the right; isReversed=false, no swap needed.
        var diagram = _parser.Parse("classDiagram\n    Dog --|> Animal");

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal(false, edge.Metadata["class:operatorReversed"]);
    }
}
