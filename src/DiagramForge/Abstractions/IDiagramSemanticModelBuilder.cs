using DiagramForge.Models;

namespace DiagramForge.Abstractions;

/// <summary>
/// Builds a <see cref="Diagram"/> semantic model incrementally.
/// Parsers use this builder to translate syntax-specific constructs
/// into the unified model without directly constructing <see cref="Diagram"/> objects.
/// </summary>
public interface IDiagramSemanticModelBuilder
{
    /// <summary>Sets the optional diagram title.</summary>
    IDiagramSemanticModelBuilder WithTitle(string title);

    /// <summary>Specifies the source syntax identifier (e.g., "mermaid").</summary>
    IDiagramSemanticModelBuilder WithSourceSyntax(string syntaxId);

    /// <summary>Specifies the diagram type identifier (for example a Mermaid diagram kind such as "flowchart", "mindmap", "sequencediagram", or a Conceptual kind such as "matrix" or "pyramid").</summary>
    IDiagramSemanticModelBuilder WithDiagramType(string diagramType);

    /// <summary>Adds a node to the diagram.</summary>
    IDiagramSemanticModelBuilder AddNode(Node node);

    /// <summary>Adds an edge between two existing nodes.</summary>
    IDiagramSemanticModelBuilder AddEdge(Edge edge);

    /// <summary>Adds a group / container to the diagram.</summary>
    IDiagramSemanticModelBuilder AddGroup(Group group);

    /// <summary>Configures layout hints for the diagram.</summary>
    IDiagramSemanticModelBuilder WithLayoutHints(LayoutHints hints);

    /// <summary>Applies a theme override to the diagram.</summary>
    IDiagramSemanticModelBuilder WithTheme(Theme theme);

    /// <summary>Constructs and returns the final <see cref="Diagram"/> model.</summary>
    Diagram Build();
}
