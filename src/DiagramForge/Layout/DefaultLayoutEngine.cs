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

        if (string.Equals(diagram.DiagramType, "timeline", StringComparison.OrdinalIgnoreCase))
        {
            LayoutTimelineDiagram(diagram, theme, minW, nodeH, hGap, vGap, pad);
            return;
        }

        if (string.Equals(diagram.DiagramType, "architecture", StringComparison.OrdinalIgnoreCase))
        {
            LayoutArchitectureDiagram(diagram, theme, minW, nodeH, hGap, vGap, pad);
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
        ShiftDiagramForGroupPadding(diagram);
    }

    private static void LayoutArchitectureDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double hGap,
        double vGap,
        double pad)
    {
        // ── Sizing pass ───────────────────────────────────────────────────────
        foreach (var node in diagram.Nodes.Values)
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            double textW = EstimateTextWidth(node.Label.Text, fontSize);
            // Junctions (Shape.Circle, no label) get a small fixed size.
            if (node.Shape == Models.Shape.Circle && string.IsNullOrEmpty(node.Label.Text))
            {
                node.Width = 20;
                node.Height = 20;
            }
            else
            {
                node.Width = Math.Max(minW, textW + 2 * theme.NodePadding);
                node.Height = nodeH;
            }
        }

        // ── Constraint-based grid positioning ────────────────────────────────
        // Build a grid position map using edge port directions as spatial constraints.
        // L/R implies horizontal adjacency; T/B implies vertical adjacency.
        // We assign (gridCol, gridRow) to each node, then convert to pixel coordinates.

        var gridCol = new Dictionary<string, int>(StringComparer.Ordinal);
        var gridRow = new Dictionary<string, int>(StringComparer.Ordinal);

        // Seed the first node (alphabetically for determinism) at (0, 0).
        if (diagram.Nodes.Count > 0)
        {
            var firstId = diagram.Nodes.Keys.OrderBy(k => k, StringComparer.Ordinal).First();
            gridCol[firstId] = 0;
            gridRow[firstId] = 0;
        }

        // Process edges iteratively; multiple passes handle chains.
        bool changed = true;
        int maxPasses = diagram.Edges.Count + 1;
        for (int pass = 0; pass < maxPasses && changed; pass++)
        {
            changed = false;
            foreach (var edge in diagram.Edges)
            {
                if (!edge.Metadata.TryGetValue("source:port", out var srcPortObj)
                    || !edge.Metadata.TryGetValue("target:port", out var dstPortObj))
                    continue;

                var srcPort = srcPortObj is string s1 ? s1 : srcPortObj?.ToString() ?? string.Empty;
                var dstPort = dstPortObj is string s2 ? s2 : dstPortObj?.ToString() ?? string.Empty;
                var srcId = edge.SourceId;
                var dstId = edge.TargetId;

                bool hasSrc = gridCol.ContainsKey(srcId);
                bool hasDst = gridCol.ContainsKey(dstId);

                if (!hasSrc && !hasDst)
                    continue;

                // Determine the relative offset implied by the port pair.
                // srcPort is the port on the source side; dstPort is the port on the target side.
                // Example: src:R -- L:dst → src is to the left of dst → dst.col = src.col + 1
                int dColOffset = 0, dRowOffset = 0;
                if ((srcPort == "R" && dstPort == "L") || (srcPort == "L" && dstPort == "R"))
                    dColOffset = srcPort == "R" ? 1 : -1;
                else if ((srcPort == "B" && dstPort == "T") || (srcPort == "T" && dstPort == "B"))
                    dRowOffset = srcPort == "B" ? 1 : -1;

                if (dColOffset == 0 && dRowOffset == 0)
                    continue; // 90° edge or unrecognised ports — skip for now

                if (hasSrc && !hasDst)
                {
                    gridCol[dstId] = gridCol[srcId] + dColOffset;
                    gridRow[dstId] = gridRow[srcId] + dRowOffset;
                    changed = true;
                }
                else if (hasDst && !hasSrc)
                {
                    gridCol[srcId] = gridCol[dstId] - dColOffset;
                    gridRow[srcId] = gridRow[dstId] - dRowOffset;
                    changed = true;
                }
            }
        }

        // Assign any still-unpositioned nodes using BFS from already-positioned nodes,
        // or to a new row at the bottom if completely disconnected.
        int nextFallbackRow = gridRow.Count > 0 ? gridRow.Values.Max() + 2 : 0;
        int fallbackCol = 0;
        foreach (var id in diagram.Nodes.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!gridCol.ContainsKey(id))
            {
                gridCol[id] = fallbackCol++;
                gridRow[id] = nextFallbackRow;
            }
        }

        // ── Shift grid so minimum col/row is 0 ───────────────────────────────
        int minCol = gridCol.Values.Min();
        int minRow = gridRow.Values.Min();
        foreach (var id in gridCol.Keys.ToList())
        {
            gridCol[id] -= minCol;
            gridRow[id] -= minRow;
        }

        // ── Compute per-column widths and per-row heights ─────────────────────
        int totalCols = gridCol.Values.Max() + 1;
        int totalRows = gridRow.Values.Max() + 1;

        var colWidths = new double[totalCols];
        var rowHeights = new double[totalRows];

        foreach (var (id, node) in diagram.Nodes)
        {
            int col = gridCol[id];
            int row = gridRow[id];
            colWidths[col] = Math.Max(colWidths[col], node.Width);
            rowHeights[row] = Math.Max(rowHeights[row], node.Height);
        }

        // ── Pixel positions ───────────────────────────────────────────────────
        var colX = new double[totalCols];
        double runX = pad;
        for (int c = 0; c < totalCols; c++)
        {
            colX[c] = runX;
            runX += colWidths[c] + hGap;
        }

        var rowY = new double[totalRows];
        double runY = pad;
        for (int r = 0; r < totalRows; r++)
        {
            rowY[r] = runY;
            runY += rowHeights[r] + vGap;
        }

        foreach (var (id, node) in diagram.Nodes)
        {
            int col = gridCol[id];
            int row = gridRow[id];
            node.X = colX[col] + (colWidths[col] - node.Width) / 2;
            node.Y = rowY[row] + (rowHeights[row] - node.Height) / 2;
        }

        // ── Group bounding boxes ──────────────────────────────────────────────
        // Recursively collect all descendant node IDs for a group (including nested groups).
        IEnumerable<string> AllNodeIds(Group g)
        {
            foreach (var nid in g.ChildNodeIds)
                yield return nid;

            foreach (var cgid in g.ChildGroupIds)
            {
                var cg = diagram.Groups.FirstOrDefault(x => x.Id == cgid);
                if (cg is not null)
                    foreach (var nid in AllNodeIds(cg))
                        yield return nid;
            }
        }

        foreach (var group in diagram.Groups)
        {
            var members = AllNodeIds(group)
                .Where(diagram.Nodes.ContainsKey)
                .Select(id => diagram.Nodes[id])
                .ToList();

            if (members.Count == 0)
            {
                group.X = 0; group.Y = 0; group.Width = 0; group.Height = 0;
                continue;
            }

            double minX = members.Min(n => n.X);
            double minY = members.Min(n => n.Y);
            double maxX = members.Max(n => n.X + n.Width);
            double maxY = members.Max(n => n.Y + n.Height);

            double sidePad = theme.NodePadding;
            bool labeled = !string.IsNullOrWhiteSpace(group.Label.Text);
            double topPad = labeled ? sidePad + theme.FontSize + 8 : sidePad;

            group.X = minX - sidePad;
            group.Y = minY - topPad;
            group.Width = (maxX - minX) + 2 * sidePad;
            group.Height = (maxY - minY) + topPad + sidePad;
        }

        // Shift whole diagram if any group extends into negative space.
        ShiftDiagramForGroupPadding(diagram);
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

    private static void LayoutTimelineDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double hGap,
        double vGap,
        double pad)
    {
        // Size all nodes from their labels.
        foreach (var node in diagram.Nodes.Values)
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            double textW = EstimateTextWidth(node.Label.Text, fontSize);
            node.Width = Math.Max(minW, textW + 2 * theme.NodePadding);
            node.Height = nodeH;
        }

        // Collect period nodes in declaration order.
        var periodNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && "period".Equals(k as string, StringComparison.Ordinal))
            .OrderBy(n => Convert.ToInt32(n.Metadata["timeline:periodIndex"], System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        if (periodNodes.Count == 0)
            return;

        // Collect event nodes grouped by period index.
        var eventNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.TryGetValue("timeline:kind", out var k) && "event".Equals(k as string, StringComparison.Ordinal))
            .ToList();

        // Use a uniform column width so all period columns are evenly spaced.
        // The column must be wide enough for the widest node in any column.
        double colWidth = periodNodes.Max(n => n.Width);
        if (eventNodes.Count > 0)
            colWidth = Math.Max(colWidth, eventNodes.Max(n => n.Width));

        // When a title is present, shift the first row down to clear it. The title
        // is rendered by SvgRenderer at y=(DiagramPadding - 4); it needs
        // (TitleFontSize + 8) of vertical room, matching the identical offset that
        // SvgRenderer.ComputeHeight already reserves at the canvas bottom.
        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;

        // Place period nodes in a single horizontal row.
        double periodY = pad + titleOffset;
        for (int i = 0; i < periodNodes.Count; i++)
        {
            var pn = periodNodes[i];
            pn.X = pad + i * (colWidth + hGap);
            pn.Y = periodY;
            pn.Width = colWidth;
        }

        // Place event nodes in columns below their owning period.
        foreach (var eventNode in eventNodes)
        {
            int pIdx = Convert.ToInt32(eventNode.Metadata["timeline:periodIndex"], System.Globalization.CultureInfo.InvariantCulture);
            int eIdx = Convert.ToInt32(eventNode.Metadata["timeline:eventIndex"], System.Globalization.CultureInfo.InvariantCulture);

            var periodNode = periodNodes.Find(n =>
                Convert.ToInt32(n.Metadata["timeline:periodIndex"], System.Globalization.CultureInfo.InvariantCulture) == pIdx);

            if (periodNode is null)
                continue;

            eventNode.X = periodNode.X;
            eventNode.Y = periodY + nodeH + vGap + eIdx * (nodeH + vGap);
            eventNode.Width = colWidth;
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

    /// <summary>
    /// Shifts all nodes and groups so that no group extends into negative coordinate space.
    /// Call this after group bounding boxes have been computed.
    /// </summary>
    private static void ShiftDiagramForGroupPadding(Diagram diagram)
    {
        if (diagram.Groups.Count == 0)
            return;

        double shiftX = Math.Max(0, -diagram.Groups.Min(g => g.X));
        double shiftY = Math.Max(0, -diagram.Groups.Min(g => g.Y));
        if (shiftX > 0 || shiftY > 0)
        {
            foreach (var n in diagram.Nodes.Values) { n.X += shiftX; n.Y += shiftY; }
            foreach (var g in diagram.Groups) { g.X += shiftX; g.Y += shiftY; }
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
