using DiagramForge.Models;
using DiagramForge.Parsers.Mermaid;

namespace DiagramForge.Tests.Parsers.Mermaid;

public class MermaidArchitectureParserTests
{
    private readonly MermaidParser _parser = new();

    // ── CanParse ──────────────────────────────────────────────────────────────

    [Fact]
    public void CanParse_ReturnsTrue_ForArchitectureBeta()
    {
        Assert.True(_parser.CanParse("architecture-beta\n  service db(database)[Database]"));
    }

    // ── DiagramType / SourceSyntax ────────────────────────────────────────────

    [Fact]
    public void Parse_SetsDiagramTypeAndSourceSyntax()
    {
        var diagram = _parser.Parse("architecture-beta\n  service db(database)[Database]");

        Assert.Equal("architecture", diagram.DiagramType);
        Assert.Equal("mermaid", diagram.SourceSyntax);
    }

    // ── Services ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Service_CreatesNodeWithLabelAndShape()
    {
        var diagram = _parser.Parse("architecture-beta\n  service db(database)[Database]");

        Assert.True(diagram.Nodes.ContainsKey("db"));
        Assert.Equal("Database", diagram.Nodes["db"].Label.Text);
        Assert.Equal(Shape.Cylinder, diagram.Nodes["db"].Shape);
    }

    [Theory]
    [InlineData("cloud", Shape.Cloud)]
    [InlineData("database", Shape.Cylinder)]
    [InlineData("disk", Shape.Cylinder)]
    [InlineData("server", Shape.Rectangle)]
    [InlineData("internet", Shape.Cloud)]
    [InlineData("unknown", Shape.Rectangle)]
    public void Parse_Service_MapsIconToShape(string icon, Shape expectedShape)
    {
        var diagram = _parser.Parse($"architecture-beta\n  service svc({icon})[Label]");

        Assert.Equal(expectedShape, diagram.Nodes["svc"].Shape);
    }

    [Fact]
    public void Parse_MultipleServices_AllCreated()
    {
        const string text = """
            architecture-beta
              service db(database)[Database]
              service server(server)[Server]
              service gateway(internet)[Gateway]
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(3, diagram.Nodes.Count);
        Assert.True(diagram.Nodes.ContainsKey("db"));
        Assert.True(diagram.Nodes.ContainsKey("server"));
        Assert.True(diagram.Nodes.ContainsKey("gateway"));
    }

    // ── Groups ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Group_CreatesGroupWithLabel()
    {
        var diagram = _parser.Parse("architecture-beta\n  group api(cloud)[API]");

        var group = Assert.Single(diagram.Groups);
        Assert.Equal("api", group.Id);
        Assert.Equal("API", group.Label.Text);
    }

    [Fact]
    public void Parse_ServiceInGroup_AddsNodeToGroupChildNodeIds()
    {
        const string text = """
            architecture-beta
              group api(cloud)[API]
              service db(database)[Database] in api
            """;

        var diagram = _parser.Parse(text);

        var group = Assert.Single(diagram.Groups);
        Assert.Equal("api", group.Id);
        Assert.Contains("db", group.ChildNodeIds);
    }

    [Fact]
    public void Parse_NestedGroup_AddsChildGroupToParent()
    {
        const string text = """
            architecture-beta
              group public_api(cloud)[Public API]
              group private_api(cloud)[Private API] in public_api
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(2, diagram.Groups.Count);
        var parent = diagram.Groups.Single(g => g.Id == "public_api");
        Assert.Contains("private_api", parent.ChildGroupIds);
    }

    // ── Junctions ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Junction_CreatesCircleNodeWithEmptyLabel()
    {
        var diagram = _parser.Parse("architecture-beta\n  junction junctionCenter");

        Assert.True(diagram.Nodes.ContainsKey("junctionCenter"));
        Assert.Equal(Shape.Circle, diagram.Nodes["junctionCenter"].Shape);
        Assert.Equal(string.Empty, diagram.Nodes["junctionCenter"].Label.Text);
    }

    // ── Edges ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_UndirectedEdge_CreatesEdgeWithNoArrowHead()
    {
        const string text = """
            architecture-beta
              service db(database)[Database]
              service server(server)[Server]
              db:L -- R:server
            """;

        var diagram = _parser.Parse(text);

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("db", edge.SourceId);
        Assert.Equal("server", edge.TargetId);
        Assert.Equal(ArrowHeadStyle.None, edge.ArrowHead);
    }

    [Fact]
    public void Parse_DirectedEdge_CreatesEdgeWithArrowHead()
    {
        const string text = """
            architecture-beta
              service subnet(internet)[Subnet]
              service gateway(internet)[Gateway]
              subnet:R --> L:gateway
            """;

        var diagram = _parser.Parse(text);

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("subnet", edge.SourceId);
        Assert.Equal("gateway", edge.TargetId);
        Assert.Equal(ArrowHeadStyle.Arrow, edge.ArrowHead);
    }

    [Fact]
    public void Parse_Edge_StoresPortDirectionsInMetadata()
    {
        const string text = """
            architecture-beta
              service db(database)[Database]
              service server(server)[Server]
              db:L -- R:server
            """;

        var diagram = _parser.Parse(text);

        var edge = diagram.Edges[0];
        Assert.Equal("L", edge.Metadata["source:port"]);
        Assert.Equal("R", edge.Metadata["target:port"]);
    }

    [Fact]
    public void Parse_VerticalEdge_StoresTopBottomPorts()
    {
        const string text = """
            architecture-beta
              service disk1(disk)[Storage]
              service server(server)[Server]
              disk1:T -- B:server
            """;

        var diagram = _parser.Parse(text);

        var edge = diagram.Edges[0];
        Assert.Equal("T", edge.Metadata["source:port"]);
        Assert.Equal("B", edge.Metadata["target:port"]);
    }

    [Fact]
    public void Parse_GroupEdgeSyntax_StripsGroupSuffixAndCreatesEdge()
    {
        const string text = """
            architecture-beta
              service server(server)[Server]
              service subnet(internet)[Subnet]
              server{group}:B --> T:subnet{group}
            """;

        var diagram = _parser.Parse(text);

        var edge = Assert.Single(diagram.Edges);
        Assert.Equal("server", edge.SourceId);
        Assert.Equal("subnet", edge.TargetId);
        Assert.Equal(ArrowHeadStyle.Arrow, edge.ArrowHead);
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Comments_AreIgnored()
    {
        const string text = """
            architecture-beta
              %% this is a comment
              service db(database)[Database]
            """;

        var diagram = _parser.Parse(text);

        Assert.Single(diagram.Nodes);
        Assert.Equal("db", diagram.Nodes.Keys.First());
    }

    // ── Full example from docs ────────────────────────────────────────────────

    [Fact]
    public void Parse_FullDocsExample_ProducesExpectedModel()
    {
        const string text = """
            architecture-beta
              group api(cloud)[API]

              service db(database)[Database] in api
              service disk1(disk)[Storage] in api
              service disk2(disk)[Storage] in api
              service server(server)[Server] in api

              db:L -- R:server
              disk1:T -- B:server
              disk2:T -- B:db
            """;

        var diagram = _parser.Parse(text);

        Assert.Equal(4, diagram.Nodes.Count);
        Assert.Single(diagram.Groups);
        Assert.Equal(3, diagram.Edges.Count);

        var group = diagram.Groups[0];
        Assert.Equal("api", group.Id);
        Assert.Equal(4, group.ChildNodeIds.Count);

        // Check node shapes
        Assert.Equal(Shape.Cylinder, diagram.Nodes["db"].Shape);
        Assert.Equal(Shape.Cylinder, diagram.Nodes["disk1"].Shape);
        Assert.Equal(Shape.Cylinder, diagram.Nodes["disk2"].Shape);
        Assert.Equal(Shape.Rectangle, diagram.Nodes["server"].Shape);
    }
}
