using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge;

/// <summary>
/// Default implementation of <see cref="IDiagramSemanticModelBuilder"/>.
/// </summary>
public sealed class DiagramSemanticModelBuilder : IDiagramSemanticModelBuilder
{
    private readonly Diagram _diagram = new();

    public IDiagramSemanticModelBuilder WithTitle(string title)
    {
        _diagram.Title = title;
        return this;
    }

    public IDiagramSemanticModelBuilder WithSourceSyntax(string syntaxId)
    {
        _diagram.SourceSyntax = syntaxId;
        return this;
    }

    public IDiagramSemanticModelBuilder WithDiagramType(string diagramType)
    {
        _diagram.DiagramType = diagramType;
        return this;
    }

    public IDiagramSemanticModelBuilder AddNode(Node node)
    {
        _diagram.AddNode(node);
        return this;
    }

    public IDiagramSemanticModelBuilder AddEdge(Edge edge)
    {
        _diagram.AddEdge(edge);
        return this;
    }

    public IDiagramSemanticModelBuilder AddGroup(Group group)
    {
        _diagram.AddGroup(group);
        return this;
    }

    public IDiagramSemanticModelBuilder WithLayoutHints(LayoutHints hints)
    {
        _diagram.LayoutHints = hints;
        return this;
    }

    public IDiagramSemanticModelBuilder WithTheme(Theme theme)
    {
        _diagram.Theme = theme;
        return this;
    }

    public Diagram Build() => _diagram;
}
