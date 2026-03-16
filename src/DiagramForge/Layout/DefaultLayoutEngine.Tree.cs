using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private const double TreeHGap = 30;
    private const double TreeVGap = 50;

    private static void LayoutTreeDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        if (diagram.Nodes.Count == 0)
            return;

        bool isOrgChart = diagram.Nodes.Values
            .Any(n => n.Metadata.ContainsKey("tree:orgchart"));

        double orgMinWidth = isOrgChart ? 120 : minW;

        // ── Build adjacency: parent → children ────────────────────────────────
        var childrenOf = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var hasParent = new HashSet<string>(StringComparer.Ordinal);

        foreach (var edge in diagram.Edges)
        {
            if (!childrenOf.TryGetValue(edge.SourceId, out var list))
            {
                list = [];
                childrenOf[edge.SourceId] = list;
            }
            list.Add(edge.TargetId);
            hasParent.Add(edge.TargetId);
        }

        // Roots are nodes with no incoming edges, ordered by their numeric suffix
        // to preserve the parser's insertion order (node_0, node_1, …, node_10, …).
        var roots = diagram.Nodes.Values
            .Where(n => !hasParent.Contains(n.Id))
            .OrderBy(n => TryParseNodeIndex(n.Id))
            .ToList();

        if (roots.Count == 0)
            return;

        // ── Sizing pass ───────────────────────────────────────────────────────
        foreach (var node in diagram.Nodes.Values)
            SizeStandardNode(node, theme, orgMinWidth, nodeH);

        // ── Compute subtree widths (bottom-up) ────────────────────────────────
        var subtreeWidth = new Dictionary<string, double>(StringComparer.Ordinal);

        double ComputeSubtreeWidth(string nodeId)
        {
            var node = diagram.Nodes[nodeId];
            if (!childrenOf.TryGetValue(nodeId, out var children) || children.Count == 0)
            {
                subtreeWidth[nodeId] = node.Width;
                return node.Width;
            }

            double childrenTotalWidth = 0;
            foreach (var childId in children)
                childrenTotalWidth += ComputeSubtreeWidth(childId);

            // Add gaps between children
            childrenTotalWidth += (children.Count - 1) * TreeHGap;

            subtreeWidth[nodeId] = Math.Max(node.Width, childrenTotalWidth);
            return subtreeWidth[nodeId];
        }

        foreach (var root in roots)
            ComputeSubtreeWidth(root.Id);

        // ── X assignment (top-down, centering children under parent) ──────────
        void AssignX(string nodeId, double leftX)
        {
            var node = diagram.Nodes[nodeId];
            double mySubtreeW = subtreeWidth[nodeId];

            // Center this node in its allocated subtree span
            node.X = leftX + (mySubtreeW - node.Width) / 2;

            if (!childrenOf.TryGetValue(nodeId, out var children) || children.Count == 0)
                return;

            // Distribute children within the subtree span
            double childX = leftX;
            foreach (var childId in children)
            {
                AssignX(childId, childX);
                childX += subtreeWidth[childId] + TreeHGap;
            }
        }

        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;

        // Lay out roots side-by-side
        double currentX = pad;
        foreach (var root in roots)
        {
            AssignX(root.Id, currentX);
            currentX += subtreeWidth[root.Id] + TreeHGap;
        }

        // ── Y assignment (depth-based layers) ─────────────────────────────────
        void AssignY(string nodeId, int depth)
        {
            var node = diagram.Nodes[nodeId];
            node.Y = pad + titleOffset + depth * (nodeH + TreeVGap);

            if (childrenOf.TryGetValue(nodeId, out var children))
            {
                foreach (var childId in children)
                    AssignY(childId, depth + 1);
            }
        }

        foreach (var root in roots)
            AssignY(root.Id, 0);
    }
}
