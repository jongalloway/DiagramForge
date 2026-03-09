using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Layout;

/// <summary>
/// A simple deterministic layout engine that arranges nodes in a top-to-bottom
/// or left-to-right grid based on the diagram's <see cref="LayoutHints"/>.
/// </summary>
/// <remarks>
/// This engine uses a breadth-first traversal of the edge graph to assign
/// nodes to rows/columns, providing a clean, readable layout for simple diagrams.
/// For more complex layouts, a future implementation could use a proper graph
/// layout algorithm (e.g., Sugiyama / ELK).
/// </remarks>
public sealed class DefaultLayoutEngine : ILayoutEngine
{
    /// <inheritdoc/>
    public void Layout(Diagram diagram, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(theme);

        if (diagram.Nodes.Count == 0)
            return;

        var hints = diagram.LayoutHints;
        double nodeW = hints.MinNodeWidth;
        double nodeH = hints.MinNodeHeight;
        double hGap = hints.HorizontalSpacing;
        double vGap = hints.VerticalSpacing;
        double pad = theme.DiagramPadding;

        // Assign nodes to layers via BFS
        var layers = ComputeLayers(diagram);

        bool isHorizontal = hints.Direction is LayoutDirection.LeftToRight
                                               or LayoutDirection.RightToLeft;

        for (int layerIdx = 0; layerIdx < layers.Count; layerIdx++)
        {
            var layer = layers[layerIdx];
            for (int nodeIdx = 0; nodeIdx < layer.Count; nodeIdx++)
            {
                var node = layer[nodeIdx];

                if (isHorizontal)
                {
                    node.X = pad + layerIdx * (nodeW + hGap);
                    node.Y = pad + nodeIdx * (nodeH + vGap);
                }
                else
                {
                    node.X = pad + nodeIdx * (nodeW + hGap);
                    node.Y = pad + layerIdx * (nodeH + vGap);
                }

                node.Width = nodeW;
                node.Height = nodeH;
            }
        }

        // Reverse positions for RL / BT
        if (hints.Direction == LayoutDirection.RightToLeft
            || hints.Direction == LayoutDirection.BottomToTop)
        {
            double maxCoord = isHorizontal
                ? diagram.Nodes.Values.Max(n => n.X) + nodeW + pad
                : diagram.Nodes.Values.Max(n => n.Y) + nodeH + pad;

            foreach (var node in diagram.Nodes.Values)
            {
                if (isHorizontal)
                    node.X = maxCoord - node.X - nodeW;
                else
                    node.Y = maxCoord - node.Y - nodeH;
            }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Assigns each node to a layer (rank) using BFS from root nodes.
    /// Nodes with no incoming edges are treated as roots (layer 0).
    /// Disconnected nodes are appended after the final layer.
    /// </summary>
    private static List<List<Node>> ComputeLayers(Diagram diagram)
    {
        // Compute in-degree for each node
        var inDegree = diagram.Nodes.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (var edge in diagram.Edges)
        {
            if (inDegree.ContainsKey(edge.TargetId))
                inDegree[edge.TargetId]++;
        }

        // Build adjacency list
        var adj = diagram.Nodes.Keys.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (var edge in diagram.Edges)
        {
            if (adj.ContainsKey(edge.SourceId))
                adj[edge.SourceId].Add(edge.TargetId);
        }

        // BFS / topological layering (Kahn's algorithm)
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        foreach (var (id, deg) in inDegree)
        {
            if (deg == 0)
            {
                queue.Enqueue(id);
                rank[id] = 0;
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in adj[current])
            {
                int newRank = rank[current] + 1;
                if (!rank.TryGetValue(neighbor, out int existing) || newRank > existing)
                    rank[neighbor] = newRank;

                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        // Assign any remaining nodes (cycles or disconnected) to the next available rank
        int maxRank = rank.Count > 0 ? rank.Values.Max() : 0;
        foreach (var id in diagram.Nodes.Keys)
        {
            if (!rank.ContainsKey(id))
                rank[id] = ++maxRank;
        }

        // Group nodes by rank
        int totalLayers = rank.Values.Max() + 1;
        var layers = Enumerable.Range(0, totalLayers).Select(_ => new List<Node>()).ToList();

        foreach (var (id, r) in rank)
        {
            layers[r].Add(diagram.Nodes[id]);
        }

        return layers;
    }
}
