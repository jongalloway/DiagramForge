using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Conceptual;

public sealed partial class ConceptualDslParser
{
    private static void ParseTreeDiagram(string[] lines, IDiagramSemanticModelBuilder builder)
    {
        // ── Parse optional style: section ─────────────────────────────────────
        string? stylePreset = null;
        int styleLine = FindSectionLine(lines, "style");
        if (styleLine >= 0)
        {
            var trimmed = lines[styleLine].Trim();
            var colonPos = trimmed.IndexOf(':', StringComparison.Ordinal);
            if (colonPos >= 0)
            {
                var value = trimmed[(colonPos + 1)..].Trim();
                if (!string.IsNullOrEmpty(value))
                    stylePreset = value.ToLowerInvariant();
            }
        }

        // ── Parse optional colors: section ────────────────────────────────────
        var colorMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int colorsLine = FindSectionLine(lines, "colors");
        if (colorsLine >= 0)
        {
            for (int i = colorsLine + 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // Stop at the next top-level section key
                if (GetIndent(line) == 0 && trimmed.EndsWith(':') && !trimmed.StartsWith('-'))
                    break;

                // Expect "name: \"#hex\"" or "name: #hex"
                var sep = trimmed.IndexOf(':', StringComparison.Ordinal);
                if (sep > 0)
                {
                    var key = trimmed[..sep].Trim();
                    var val = trimmed[(sep + 1)..].Trim().Trim('"');
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                        colorMap[key] = val;
                }
            }
        }

        // ── Parse required tree: section ──────────────────────────────────────
        int treeLine = FindSectionLine(lines, "tree");
        if (treeLine < 0)
            throw new DiagramParseException("Missing required section 'tree:' in tree diagram.");

        int baseIndent = -1;
        var stack = new Stack<(int indent, string nodeId)>();
        int nodeCounter = 0;
        bool isOrgChart = string.Equals(stylePreset, "orgchart", StringComparison.OrdinalIgnoreCase);

        for (int i = treeLine + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Stop at next top-level section key
            if (GetIndent(line) == 0 && trimmed.EndsWith(':') && !trimmed.StartsWith('-'))
                break;

            int indent = GetIndent(line);
            if (baseIndent < 0)
                baseIndent = indent;

            // Normalize indent relative to base
            int relIndent = indent - baseIndent;

            // Parse optional [color-group] tag
            string label = trimmed;
            string? colorGroup = null;
            int bracketStart = trimmed.LastIndexOf('[');
            int bracketEnd = trimmed.LastIndexOf(']');
            if (bracketStart >= 0 && bracketEnd > bracketStart)
            {
                colorGroup = trimmed[(bracketStart + 1)..bracketEnd].Trim();
                label = trimmed[..bracketStart].Trim();
            }

            if (string.IsNullOrEmpty(label))
                continue;

            var nodeId = $"node_{nodeCounter++}";
            var node = new Node(nodeId, label);

            // Apply fill color from color map
            if (colorGroup is not null && colorMap.TryGetValue(colorGroup, out var color))
                node.FillColor = color;

            // Store tree metadata – depth equals the current ancestor count
            // (i.e. the stack size after popping), which is independent of indent width.
            node.Metadata["tree:depth"] = stack.Count;
            if (isOrgChart)
                node.Metadata["tree:orgchart"] = true;

            builder.AddNode(node);

            // Pop stack until we find a parent with strictly smaller indentation
            while (stack.Count > 0 && stack.Peek().indent >= relIndent)
                stack.Pop();

            if (stack.Count > 0)
            {
                var edge = new Edge(stack.Peek().nodeId, nodeId)
                {
                    Routing = EdgeRouting.Orthogonal,
                    ArrowHead = ArrowHeadStyle.None,
                };
                edge.Metadata["tree:edge"] = true;
                builder.AddEdge(edge);
            }

            stack.Push((relIndent, nodeId));
        }

        if (nodeCounter == 0)
            throw new DiagramParseException("Section 'tree' contains no items.");

        builder.WithLayoutHints(new LayoutHints { Direction = LayoutDirection.TopToBottom });
    }
}
