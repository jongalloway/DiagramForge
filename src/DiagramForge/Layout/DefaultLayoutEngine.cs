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
    private const string BlockColumnCountKey = "block:columnCount";

    // Block diagrams use tighter node-to-node spacing than flowcharts.  Mermaid
    // renders blocks with minimal gaps; these values approximate that look while
    // still leaving enough room for edge labels when they exist.
    // The outer diagram padding (left/top/right/bottom) deliberately reuses
    // theme.DiagramPadding so all four sides are symmetric.
    private const double BlockHGap = 8;
    private const double BlockVGap = 8;

    /// <summary>
    /// Average glyph advance as a fraction of font size (em-units) for typical
    /// Latin UI sans-serif stacks (Segoe UI, Inter, Arial). Derived empirically;
    /// biased slightly high so shapes err on the side of too-wide rather than
    /// clipping text. A proper font-metrics engine would replace this heuristic
    /// when higher fidelity is needed.
    /// </summary>
    private const double AvgGlyphAdvanceEm = 0.6;

    /// <inheritdoc/>
    public void Layout(Diagram diagram, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(theme);

        if (diagram.Nodes.Count == 0)
            return;

        var hints = diagram.LayoutHints;
        double minW = hints.MinNodeWidth;
        double nodeH = hints.MinNodeHeight;
        double hGap = hints.HorizontalSpacing;
        double vGap = hints.VerticalSpacing;
        double pad = theme.DiagramPadding;

        if (string.Equals(diagram.DiagramType, "block", StringComparison.OrdinalIgnoreCase))
        {
            LayoutBlockDiagram(diagram, theme, minW, nodeH, BlockHGap, BlockVGap, pad);
            return;
        }

        if (string.Equals(diagram.DiagramType, "sequencediagram", StringComparison.OrdinalIgnoreCase))
        {
            LayoutSequenceDiagram(diagram, theme, minW, nodeH, hGap, vGap, pad);
            return;
        }

        // ── Sizing pass ───────────────────────────────────────────────────────
        // Compute each node's width from its label so text does not overflow the
        // shape. MinNodeWidth remains a floor so short labels ("A", "End") do not
        // produce skinny boxes.

        foreach (var node in diagram.Nodes.Values)
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            double textW = EstimateTextWidth(node.Label.Text, fontSize);
            node.Width = Math.Max(minW, textW + 2 * theme.NodePadding);
            node.Height = nodeH;
        }

        // ── Positioning pass ──────────────────────────────────────────────────
        // Assign nodes to layers via BFS, then place them. Because widths are now
        // variable, we track running offsets rather than multiplying a fixed stride.
        //
        //   Horizontal (LR/RL): each layer is a vertical column. Columns advance
        //   along X by the *widest* node in the previous column + hGap. Nodes within
        //   a column stack by uniform height.
        //
        //   Vertical (TB/BT):   each layer is a horizontal row. Rows advance along
        //   Y by uniform height. Within a row, nodes advance along X by each node's
        //   own width + hGap.

        var layers = ComputeLayers(diagram);

        bool isHorizontal = hints.Direction is LayoutDirection.LeftToRight
                                               or LayoutDirection.RightToLeft;

        if (isHorizontal)
        {
            double columnX = pad;
            foreach (var layer in layers)
            {
                // ComputeLayers can emit an empty layer when every node is in a cycle
                // (no in-degree-zero roots → BFS never starts → fallback ranking leaves
                // layer 0 unpopulated). The old fixed-stride loop tolerated this by
                // running zero inner iterations; do the same here.
                if (layer.Count == 0)
                    continue;

                double maxWidthInColumn = layer.Max(n => n.Width);
                for (int i = 0; i < layer.Count; i++)
                {
                    var node = layer[i];
                    node.X = columnX;
                    node.Y = pad + i * (nodeH + vGap);
                }
                columnX += maxWidthInColumn + hGap;
            }
        }
        else
        {
            double rowY = pad;
            foreach (var layer in layers)
            {
                if (layer.Count == 0)
                    continue; // see comment above

                double runX = pad;
                foreach (var node in layer)
                {
                    node.X = runX;
                    node.Y = rowY;
                    runX += node.Width + hGap;
                }
                rowY += nodeH + vGap;
            }
        }

        // ── Reversal for RL / BT ──────────────────────────────────────────────
        // Mirror coordinates along the major axis. Must use each node's own width
        // (not a fixed constant) so variable-width nodes stay inside the frame.

        if (hints.Direction == LayoutDirection.RightToLeft
            || hints.Direction == LayoutDirection.BottomToTop)
        {
            if (isHorizontal)
            {
                double frameW = diagram.Nodes.Values.Max(n => n.X + n.Width) + pad;
                foreach (var node in diagram.Nodes.Values)
                {
                    node.X = frameW - node.X - node.Width;
                }
            }
            else
            {
                double frameH = diagram.Nodes.Values.Max(n => n.Y + n.Height) + pad;
                foreach (var node in diagram.Nodes.Values)
                {
                    node.Y = frameH - node.Y - node.Height;
                }
            }
        }

        // ── Group bounding boxes ──────────────────────────────────────────────────
        // Compute each group's frame from its member nodes' final positions. Must
        // run after the RL/BT mirror so group rects don't end up on the wrong side.
        // This is deliberately a post-hoc fit rather than group-aware positioning:
        // members of different groups can interleave in the same BFS layer and the
        // resulting rects may overlap. That's an accepted v1 limitation (tracked in
        // #14) — real-world subgraphs tend to be naturally clustered in the source.

        foreach (var group in diagram.Groups)
        {
            var members = group.ChildNodeIds
                .Where(diagram.Nodes.ContainsKey)
                .Select(id => diagram.Nodes[id])
                .ToList();

            if (members.Count == 0)
            {
                // Reset to zero so that a Diagram that is laid out more than once
                // does not carry stale bounds from a previous pass.
                group.X = 0;
                group.Y = 0;
                group.Width = 0;
                group.Height = 0;
                continue; // leave at zero-size; renderer emits an invisible <rect>
            }

            double minX = members.Min(n => n.X);
            double minY = members.Min(n => n.Y);
            double maxX = members.Max(n => n.X + n.Width);
            double maxY = members.Max(n => n.Y + n.Height);

            // Top padding reserves room for the group label (SvgRenderer draws it at
            // group.Y + fontSize + 4). Unlabeled groups — anonymous `subgraph` blocks
            // — use uniform padding so the rect is a snug frame.
            double sidePad = theme.NodePadding;
            bool labeled = !string.IsNullOrWhiteSpace(group.Label.Text);
            double topPad = labeled ? sidePad + theme.FontSize + 8 : sidePad;

            group.X = minX - sidePad;
            group.Y = minY - topPad;
            group.Width = (maxX - minX) + 2 * sidePad;
            group.Height = (maxY - minY) + topPad + sidePad;
        }

        // Shift the whole diagram if any group extends into negative space. This
        // happens when a group member sits in the first row/column and the group's
        // own padding (especially the label-height top inset) exceeds DiagramPadding.
        // Right/bottom are handled separately by SvgRenderer.ComputeWidth/Height
        // including group extents.
        if (diagram.Groups.Count > 0)
        {
            double shiftX = Math.Max(0, -diagram.Groups.Min(g => g.X));
            double shiftY = Math.Max(0, -diagram.Groups.Min(g => g.Y));
            if (shiftX > 0 || shiftY > 0)
            {
                foreach (var n in diagram.Nodes.Values) { n.X += shiftX; n.Y += shiftY; }
                foreach (var g in diagram.Groups) { g.X += shiftX; g.Y += shiftY; }
            }
        }
    }

    private static void LayoutSequenceDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double hGap,
        double vGap,
        double pad)
    {
        // Size each participant node from its label.
        foreach (var node in diagram.Nodes.Values)
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            double textW = EstimateTextWidth(node.Label.Text, fontSize);
            node.Width = Math.Max(minW, textW + 2 * theme.NodePadding);
            node.Height = nodeH;
        }

        // Order participants by their declared index (stored during parsing).
        // ThenBy ensures deterministic output when the index is missing or two
        // participants share the same value (e.g., programmatically-built diagrams).
        var ordered = diagram.Nodes.Values
            .OrderBy(n => n.Metadata.TryGetValue("sequence:participantIndex", out var v)
                ? Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture)
                : int.MaxValue)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        // Place participants in a single row across the top of the diagram.
        double runX = pad;
        foreach (var node in ordered)
        {
            node.X = runX;
            node.Y = pad;
            runX += node.Width + hGap;
        }

        // Assign each message edge its own Y row below the participant strip.
        // Each row is vGap tall, giving space for the arrow line and any label above it.
        double firstMessageY = pad + nodeH + vGap / 2;
        double messageRowHeight = vGap;

        foreach (var edge in diagram.Edges)
        {
            int msgIdx = edge.Metadata.TryGetValue("sequence:messageIndex", out var idxObj)
                ? Convert.ToInt32(idxObj, System.Globalization.CultureInfo.InvariantCulture)
                : 0;
            edge.Metadata["sequence:messageY"] = firstMessageY + msgIdx * messageRowHeight;
        }

        // Store the canvas height needed to fit all message rows so the renderer
        // can size the SVG correctly (node Y extents alone would clip the messages).
        int edgeCount = diagram.Edges.Count;
        double canvasHeight = firstMessageY + Math.Max(0, edgeCount) * messageRowHeight + pad;
        diagram.Metadata["sequence:canvasHeight"] = canvasHeight;
    }

    private static void LayoutBlockDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double hGap,
        double vGap,
        double pad)
    {
        foreach (var node in diagram.Nodes.Values)
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            double textW = EstimateTextWidth(node.Label.Text, fontSize);
            bool isArrow = node.Shape is Shape.ArrowRight or Shape.ArrowLeft or Shape.ArrowUp or Shape.ArrowDown;

            node.Width = Math.Max(isArrow ? minW * 0.75 : minW, textW + 2 * theme.NodePadding);
            node.Height = isArrow ? nodeH + 8 : nodeH;
        }

        var placedNodes = diagram.Nodes.Values
            .Where(node => node.Metadata.ContainsKey("block:row") && node.Metadata.ContainsKey("block:column"))
            .ToList();

        if (placedNodes.Count == 0)
            return;

        int rowCount = placedNodes.Max(node => Convert.ToInt32(node.Metadata["block:row"], System.Globalization.CultureInfo.InvariantCulture)) + 1;
        double baseColumnWidth = Math.Max(
            minW,
            placedNodes.Max(node =>
            {
                int span = node.Metadata.TryGetValue("block:span", out var spanValue)
                    ? Convert.ToInt32(spanValue, System.Globalization.CultureInfo.InvariantCulture)
                    : 1;
                return node.Width / Math.Max(1, span);
            }));

        var rowHeights = Enumerable.Repeat(nodeH, rowCount).ToArray();
        foreach (var node in placedNodes)
        {
            int row = Convert.ToInt32(node.Metadata["block:row"], System.Globalization.CultureInfo.InvariantCulture);
            rowHeights[row] = Math.Max(rowHeights[row], node.Height);
        }

        var rowStarts = new double[rowCount];
        double currentY = pad;
        for (int row = 0; row < rowCount; row++)
        {
            rowStarts[row] = currentY;
            currentY += rowHeights[row] + vGap;
        }

        foreach (var node in placedNodes)
        {
            int row = Convert.ToInt32(node.Metadata["block:row"], System.Globalization.CultureInfo.InvariantCulture);
            int column = Convert.ToInt32(node.Metadata["block:column"], System.Globalization.CultureInfo.InvariantCulture);
            int span = node.Metadata.TryGetValue("block:span", out var spanValue)
                ? Convert.ToInt32(spanValue, System.Globalization.CultureInfo.InvariantCulture)
                : 1;

            double slotX = pad + column * (baseColumnWidth + hGap);
            double slotWidth = baseColumnWidth * span + hGap * Math.Max(0, span - 1);
            node.X = slotX;
            node.Y = rowStarts[row] + (rowHeights[row] - node.Height) / 2;
            node.Width = Math.Max(node.Width, slotWidth);
        }
    }

    // ── Text measurement ──────────────────────────────────────────────────────

    /// <summary>
    /// Estimates the rendered width of <paramref name="text"/> using a char-count
    /// heuristic: <c>length × fontSize × 0.6</c>. Server-side SVG has no DOM to
    /// query for <c>getBBox()</c>; this gets us within ±10% for Latin text, which
    /// is sufficient for layout (padding absorbs the slop).
    /// </summary>
    private static double EstimateTextWidth(string? text, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        return text.Length * fontSize * AvgGlyphAdvanceEm;
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

        // Assign any remaining nodes (cycles or disconnected) to the next available rank.
        // Iterate in stable order so layout is fully deterministic regardless of Dictionary
        // enumeration order.
        int maxRank = rank.Count > 0 ? rank.Values.Max() : 0;
        foreach (var id in diagram.Nodes.Keys.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!rank.ContainsKey(id))
                rank[id] = ++maxRank;
        }

        // Group nodes by rank — iterate in key-sorted order so nodes within each layer
        // are positioned consistently across runtimes.
        int totalLayers = rank.Values.Max() + 1;
        var layers = Enumerable.Range(0, totalLayers).Select(_ => new List<Node>()).ToList();

        foreach (var (id, r) in rank.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            layers[r].Add(diagram.Nodes[id]);
        }

        return layers;
    }
}
