using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidMindmapParser : IMermaidDiagramParser
{
    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.Mindmap;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("mindmap");

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.TopToBottom });

        var stack = new Stack<(int indent, string nodeId)>();
        int nodeCounter = 0;

        // Skip index 0 (the "mindmap" header); use RawLines to preserve indentation.
        for (int i = 1; i < document.RawLines.Length; i++)
        {
            var raw = document.RawLines[i];
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            int indent = raw.Length - raw.TrimStart().Length;
            var text = raw.Trim();
            if (string.IsNullOrEmpty(text))
                continue;

            // ParseNodeDeclaration returns a source-text ID (e.g. "root" from "root((Product))"),
            // but mindmap nodes are identified by generated IDs — the parsed ID is intentionally unused.
            var (_, label, shape) = MermaidNodeSyntax.ParseNodeDeclaration(text);

            var nodeId = $"node_{nodeCounter++}";
            var node = new Node(nodeId, label);
            if (shape.HasValue)
                node.Shape = shape.Value;

            builder.AddNode(node);

            // Pop stack until we find a parent with strictly smaller indentation.
            while (stack.Count > 0 && stack.Peek().indent >= indent)
                stack.Pop();

            if (stack.Count > 0)
                builder.AddEdge(new Edge(stack.Peek().nodeId, nodeId));

            stack.Push((indent, nodeId));
        }

        return builder.Build();
    }
}
