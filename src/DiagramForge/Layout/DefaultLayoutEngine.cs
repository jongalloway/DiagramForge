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

    // Tight gaps for block diagrams without connectors – keeps the layout
    // compact when there are no edges to route between nodes.
    private const double BlockHGapTight = 8;
    private const double BlockVGapTight = 8;

    // Wider gaps when edges are present, matching Mermaid's default
    // nodeSpacing/rankSpacing (50/50) so bezier edges have room.
    private const double BlockHGapWide = 50;
    private const double BlockVGapWide = 40;
    private const double PyramidLevelGap = 6;

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
            bool hasEdges = diagram.Edges.Count > 0;
            double blockHGap = hasEdges ? BlockHGapWide : BlockHGapTight;
            double blockVGap = hasEdges ? BlockVGapWide : BlockVGapTight;
            LayoutBlockDiagram(diagram, theme, minW, nodeH, blockHGap, blockVGap, pad);
            return;
        }

        if (string.Equals(diagram.DiagramType, "sequencediagram", StringComparison.OrdinalIgnoreCase))
        {
            LayoutSequenceDiagram(diagram, theme, minW, nodeH, hGap, vGap, pad);
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

        if (string.Equals(diagram.DiagramType, "venn", StringComparison.OrdinalIgnoreCase))
        {
            LayoutVennDiagram(diagram, theme, minW, pad);
            return;
        }

        if (string.Equals(diagram.DiagramType, "matrix", StringComparison.OrdinalIgnoreCase))
        {
            LayoutMatrixDiagram(diagram, theme, minW, nodeH, pad);
            return;
        }

        if (string.Equals(diagram.DiagramType, "pyramid", StringComparison.OrdinalIgnoreCase))
        {
            LayoutPyramidDiagram(diagram, theme, minW, nodeH, pad);
            return;
        }

        if (string.Equals(diagram.DiagramType, "cycle", StringComparison.OrdinalIgnoreCase))
        {
            LayoutCycleDiagram(diagram, theme, minW, nodeH, pad);
            return;
        }
        
        if (string.Equals(diagram.DiagramType, "xychart", StringComparison.OrdinalIgnoreCase))
        {
            LayoutXyChartDiagram(diagram, theme, pad);
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

        var groupsById = new Dictionary<string, Group>(StringComparer.Ordinal);
        foreach (var g in diagram.Groups)
        {
            if (!groupsById.TryAdd(g.Id, g))
                throw new InvalidOperationException(
                    $"Duplicate group id '{g.Id}' in diagram. Group IDs must be unique.");
        }

        var computedGroups = new HashSet<string>(StringComparer.Ordinal);

        void ComputeGroupBounds(Group group)
        {
            if (!computedGroups.Add(group.Id))
                return;

            var nodeMembers = group.ChildNodeIds
                .Where(diagram.Nodes.ContainsKey)
                .Select(id => diagram.Nodes[id])
                .ToList();

            var childGroups = group.ChildGroupIds
                .Where(groupsById.ContainsKey)
                .Select(id => groupsById[id])
                .ToList();

            foreach (var childGroup in childGroups)
                ComputeGroupBounds(childGroup);

            // Only count child groups that have non-zero bounds (empty child groups
            // do not contribute to the parent frame and must not leave min/max as ±Infinity).
            var validChildGroups = childGroups.Where(child => child.Width > 0 && child.Height > 0).ToList();

            if (nodeMembers.Count == 0 && validChildGroups.Count == 0)
            {
                // Reset to zero so that a Diagram that is laid out more than once
                // does not carry stale bounds from a previous pass.
                group.X = 0;
                group.Y = 0;
                group.Width = 0;
                group.Height = 0;
                return;
            }

            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;

            foreach (var node in nodeMembers)
            {
                minX = Math.Min(minX, node.X);
                minY = Math.Min(minY, node.Y);
                maxX = Math.Max(maxX, node.X + node.Width);
                maxY = Math.Max(maxY, node.Y + node.Height);
            }

            foreach (var childGroup in validChildGroups)
            {
                minX = Math.Min(minX, childGroup.X);
                minY = Math.Min(minY, childGroup.Y);
                maxX = Math.Max(maxX, childGroup.X + childGroup.Width);
                maxY = Math.Max(maxY, childGroup.Y + childGroup.Height);
            }

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

        foreach (var group in diagram.Groups)
            ComputeGroupBounds(group);

        // Shift the whole diagram if any group extends into negative space. This
        // happens when a group member sits in the first row/column and the group's
        // own padding (especially the label-height top inset) exceeds DiagramPadding.
        // Right/bottom are handled separately by SvgRenderer.ComputeWidth/Height
        // including group extents.
        ShiftDiagramForGroupPadding(diagram, theme.DiagramPadding);
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

                var srcPort = srcPortObj switch
                {
                    string s => s,
                    not null => srcPortObj.ToString() ?? string.Empty,
                    _ => string.Empty,
                };
                var dstPort = dstPortObj switch
                {
                    string s => s,
                    not null => dstPortObj.ToString() ?? string.Empty,
                    _ => string.Empty,
                };
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
        var groupsById = new Dictionary<string, Group>(StringComparer.Ordinal);
        foreach (var g in diagram.Groups)
        {
            if (!groupsById.TryAdd(g.Id, g))
                throw new InvalidOperationException(
                    $"Duplicate group id '{g.Id}' in diagram. Group IDs must be unique.");
        }

        var computedGroups = new HashSet<string>(StringComparer.Ordinal);

        void ComputeGroupBounds(Group group)
        {
            if (!computedGroups.Add(group.Id))
                return;

            var nodeMembers = group.ChildNodeIds
                .Where(diagram.Nodes.ContainsKey)
                .Select(id => diagram.Nodes[id])
                .ToList();

            var childGroups = group.ChildGroupIds
                .Where(groupsById.ContainsKey)
                .Select(id => groupsById[id])
                .ToList();

            foreach (var childGroup in childGroups)
                ComputeGroupBounds(childGroup);

            // Only count child groups that have non-zero bounds (empty child groups
            // do not contribute to the parent frame and must not leave min/max as ±Infinity).
            var validChildGroups = childGroups.Where(child => child.Width > 0 && child.Height > 0).ToList();

            if (nodeMembers.Count == 0 && validChildGroups.Count == 0)
            {
                group.X = 0;
                group.Y = 0;
                group.Width = 0;
                group.Height = 0;
                return;
            }

            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;

            foreach (var node in nodeMembers)
            {
                minX = Math.Min(minX, node.X);
                minY = Math.Min(minY, node.Y);
                maxX = Math.Max(maxX, node.X + node.Width);
                maxY = Math.Max(maxY, node.Y + node.Height);
            }

            foreach (var childGroup in validChildGroups)
            {
                minX = Math.Min(minX, childGroup.X);
                minY = Math.Min(minY, childGroup.Y);
                maxX = Math.Max(maxX, childGroup.X + childGroup.Width);
                maxY = Math.Max(maxY, childGroup.Y + childGroup.Height);
            }

            double sidePad = theme.NodePadding;
            bool labeled = !string.IsNullOrWhiteSpace(group.Label.Text);
            double topPad = labeled ? sidePad + theme.FontSize + 8 : sidePad;

            group.X = minX - sidePad;
            group.Y = minY - topPad;
            group.Width = (maxX - minX) + 2 * sidePad;
            group.Height = (maxY - minY) + topPad + sidePad;
        }

        foreach (var group in diagram.Groups)
            ComputeGroupBounds(group);

        // Shift whole diagram if any group extends into negative space.
        ShiftDiagramForGroupPadding(diagram, theme.DiagramPadding);
    }

    private static void LayoutPyramidDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        var orderedNodes = diagram.Nodes.Values
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .ToList();

        if (orderedNodes.Count == 0)
            return;

        double widestLabel = orderedNodes.Max(node =>
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            return EstimateTextWidth(node.Label.Text, fontSize) + 2 * theme.NodePadding;
        });

        int levelCount = orderedNodes.Count;
        double bottomWidth = Math.Max(widestLabel, minW * 1.9);
        double totalHeight = levelCount * nodeH + (levelCount - 1) * PyramidLevelGap;

        for (int index = 0; index < orderedNodes.Count; index++)
        {
            var node = orderedNodes[index];
            double yTop = index * (nodeH + PyramidLevelGap);
            double yBottom = yTop + nodeH;
            double topWidth = bottomWidth * yTop / totalHeight;
            double segmentBottomWidth = bottomWidth * yBottom / totalHeight;

            node.X = pad;
            node.Y = pad + yTop;
            node.Width = bottomWidth;
            node.Height = nodeH;
            node.Metadata["conceptual:pyramidSegment"] = true;
            node.Metadata["conceptual:pyramidTopWidth"] = topWidth;
            node.Metadata["conceptual:pyramidBottomWidth"] = segmentBottomWidth;
        }
    }

    private static void LayoutCycleDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        var orderedNodes = diagram.Nodes.Values
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .ToList();

        if (orderedNodes.Count == 0)
            return;

        // Uniform node sizing driven by the widest label.
        double widestLabel = orderedNodes.Max(node =>
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            return EstimateTextWidth(node.Label.Text, fontSize) + 2 * theme.NodePadding;
        });

        double nodeW = Math.Max(widestLabel, minW);
        int n = orderedNodes.Count;

        // Minimum radius so adjacent node bounding boxes don't overlap.
        // Chord between adjacent centres = 2·R·sin(π/n); must exceed the
        // diagonal of the node box plus a small visual gap.
        double diagonal = Math.Sqrt(nodeW * nodeW + nodeH * nodeH);
        const double minNodeGap = 20.0;
        double minRadiusFromSpacing = (diagonal + minNodeGap) / (2 * Math.Sin(Math.PI / n));

        // Also enforce a floor so even 3-step cycles have a visually open centre.
        double minRadiusFloor = nodeW * 1.2 + pad;
        double radius = Math.Max(minRadiusFromSpacing, minRadiusFloor);

        // Centre of the circle: offset so the nearest node edge lands exactly at `pad`.
        double cx = pad + radius + nodeW / 2;
        double cy = pad + radius + nodeH / 2;

        // Place nodes evenly, starting at 12 o'clock (−π/2), going clockwise.
        for (int i = 0; i < n; i++)
        {
            var node = orderedNodes[i];
            double angle = -Math.PI / 2 + (2 * Math.PI * i / n);
            node.X = cx + radius * Math.Cos(angle) - nodeW / 2;
            node.Y = cy + radius * Math.Sin(angle) - nodeH / 2;
            node.Width = nodeW;
            node.Height = nodeH;
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
            .OrderBy(n => TryGetMetadataInt(n.Metadata, "sequence:participantIndex", out var participantIndex)
                ? participantIndex
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
            int msgIdx = TryGetMetadataInt(edge.Metadata, "sequence:messageIndex", out var messageIndex)
                ? messageIndex
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

        int rowCount = placedNodes.Max(node => GetMetadataInt(node.Metadata, "block:row")) + 1;
        double baseColumnWidth = Math.Max(
            minW,
            placedNodes.Max(node =>
            {
                int span = TryGetMetadataInt(node.Metadata, "block:span", out var spanValue)
                    ? spanValue
                    : 1;
                return node.Width / Math.Max(1, span);
            }));

        var rowHeights = Enumerable.Repeat(nodeH, rowCount).ToArray();
        foreach (var node in placedNodes)
        {
            int row = GetMetadataInt(node.Metadata, "block:row");
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
            int row = GetMetadataInt(node.Metadata, "block:row");
            int column = GetMetadataInt(node.Metadata, "block:column");
            int span = TryGetMetadataInt(node.Metadata, "block:span", out var spanValue)
                ? spanValue
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
            .Where(n => MetadataEquals(n, "timeline:kind", "period"))
            .OrderBy(n => GetMetadataInt(n.Metadata, "timeline:periodIndex"))
            .ToList();

        if (periodNodes.Count == 0)
            return;

        // Collect event nodes grouped by period index.
        var eventNodes = diagram.Nodes.Values
            .Where(n => MetadataEquals(n, "timeline:kind", "event"))
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
            int pIdx = GetMetadataInt(eventNode.Metadata, "timeline:periodIndex");
            int eIdx = GetMetadataInt(eventNode.Metadata, "timeline:eventIndex");

            var periodNode = periodNodes.Find(n =>
                GetMetadataInt(n.Metadata, "timeline:periodIndex") == pIdx);

            if (periodNode is null)
                continue;

            eventNode.X = periodNode.X;
            eventNode.Y = periodY + nodeH + vGap + eIdx * (nodeH + vGap);
            eventNode.Width = colWidth;
        }
    }

    private static bool MetadataEquals(Node node, string key, string expected) =>
        node.Metadata.GetValueOrDefault(key) is string actual
        && string.Equals(actual, expected, StringComparison.Ordinal);

    private static int GetMetadataInt(Dictionary<string, object> metadata, string key) =>
        Convert.ToInt32(metadata[key], System.Globalization.CultureInfo.InvariantCulture);

    private static bool TryGetMetadataInt(Dictionary<string, object> metadata, string key, out int value)
    {
        if (metadata.TryGetValue(key, out var rawValue))
        {
            value = Convert.ToInt32(rawValue, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        value = default;
        return false;
    }

    private static void LayoutVennDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double pad)
    {
        static string? GetVennKind(Node node) => node.Metadata.GetValueOrDefault("venn:kind") as string;
        static bool IsVennKind(Node node, string kind) => string.Equals(GetVennKind(node), kind, StringComparison.Ordinal);

        var setNodes = diagram.Nodes.Values
            .Where(n => !IsVennKind(n, "overlap") && !IsVennKind(n, "text"))
            .OrderBy(n => n.Metadata.TryGetValue("venn:index", out var indexObj)
                ? Convert.ToInt32(indexObj, System.Globalization.CultureInfo.InvariantCulture)
                : int.MaxValue)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        var overlapNodes = diagram.Nodes.Values
            .Where(n => IsVennKind(n, "overlap"))
            .OrderBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        var textNodes = diagram.Nodes.Values
            .Where(n => IsVennKind(n, "text"))
            .OrderBy(n => n.Metadata.TryGetValue("venn:textIndex", out var indexObj)
                ? Convert.ToInt32(indexObj, System.Globalization.CultureInfo.InvariantCulture)
                : int.MaxValue)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        if (setNodes.Count == 0)
            return;

        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;
        double diameter = setNodes.Max(node =>
        {
            double fontSize = node.Label.FontSize ?? theme.FontSize;
            double labelWidth = EstimateTextWidth(node.Label.Text, fontSize);
            var nestedTextNodes = GetSetTextNodes(textNodes, node.Id).ToList();
            double nestedTextWidth = nestedTextNodes.Count == 0
                ? 0
                : nestedTextNodes.Max(textNode => EstimateTextWidth(textNode.Label.Text, textNode.Label.FontSize ?? theme.FontSize));
            double nestedHeightAllowance = nestedTextNodes.Count == 0
                ? 0
                : nestedTextNodes.Count * fontSize * 1.15 + fontSize * 1.8;

            return Math.Max(
                minW,
                Math.Max(
                    Math.Max(labelWidth, nestedTextWidth) + 2 * theme.NodePadding,
                    minW + nestedHeightAllowance));
        });

        foreach (var node in setNodes)
        {
            node.Shape = Shape.Circle;
            node.Width = diameter;
            node.Height = diameter;
            node.Metadata.Remove("label:centerX");
            node.Metadata.Remove("label:centerY");
        }

        foreach (var node in overlapNodes)
        {
            node.Width = 0;
            node.Height = 0;
            node.Metadata.Remove("label:centerX");
            node.Metadata.Remove("label:centerY");
        }

        foreach (var node in textNodes)
        {
            node.Width = 0;
            node.Height = 0;
            node.Metadata.Remove("label:centerX");
            node.Metadata.Remove("label:centerY");
        }

        if (setNodes.Count == 1)
        {
            setNodes[0].X = pad;
            setNodes[0].Y = pad + titleOffset;
            SetLabelCenter(setNodes[0], diameter * 0.5, diameter * 0.5);
            PositionVennTextStack(GetSetTextNodes(textNodes, setNodes[0].Id), setNodes[0].X + diameter * 0.50, setNodes[0].Y + diameter * 0.60, theme, 0.95);
            return;
        }

        if (setNodes.Count == 2)
        {
            double horizontalOffset = diameter * 0.58;
            setNodes[0].X = pad;
            setNodes[0].Y = pad + titleOffset;
            setNodes[1].X = pad + horizontalOffset;
            setNodes[1].Y = pad + titleOffset;
            SetLabelCenter(setNodes[0], diameter * 0.32, diameter * 0.5);
            SetLabelCenter(setNodes[1], diameter * 0.68, diameter * 0.5);

            PositionVennTextStack(GetSetTextNodes(textNodes, setNodes[0].Id), setNodes[0].X + diameter * 0.24, setNodes[0].Y + diameter * 0.68, theme, 0.95);
            PositionVennTextStack(GetSetTextNodes(textNodes, setNodes[1].Id), setNodes[1].X + diameter * 0.76, setNodes[1].Y + diameter * 0.68, theme, 0.95);

            double overlapAnchorX = setNodes[0].X + diameter * 0.50 + horizontalOffset * 0.50;
            double overlapAnchorY = setNodes[0].Y + diameter * 0.62;
            var overlapNode = overlapNodes.FirstOrDefault(node => string.Equals(node.Metadata.GetValueOrDefault("venn:region") as string, "ab", StringComparison.Ordinal));
            PositionVennOverlapNode(overlapNode, overlapAnchorX, overlapAnchorY, theme);
            PositionVennTextStack(
                GetRegionTextNodes(textNodes, "ab"),
                overlapAnchorX,
                overlapAnchorY + GetNestedTextOffset(overlapNode, theme),
                theme);
            return;
        }

        if (setNodes.Count == 3)
        {
            double horizontalOffset = diameter * 0.58;
            double verticalOffset = diameter * 0.50;

            var top = setNodes[0];
            var left = setNodes[1];
            var right = setNodes[2];

            top.X = pad + horizontalOffset / 2;
            top.Y = pad + titleOffset;
            left.X = pad;
            left.Y = pad + titleOffset + verticalOffset;
            right.X = pad + horizontalOffset;
            right.Y = pad + titleOffset + verticalOffset;

            SetLabelCenter(top, diameter * 0.50, diameter * 0.24);
            SetLabelCenter(left, diameter * 0.26, diameter * 0.56);
            SetLabelCenter(right, diameter * 0.74, diameter * 0.56);

            PositionVennTextStack(GetSetTextNodes(textNodes, top.Id), top.X + diameter * 0.50, top.Y + diameter * 0.46, theme, 0.90);
            PositionVennTextStack(GetSetTextNodes(textNodes, left.Id), left.X + diameter * 0.18, left.Y + diameter * 0.68, theme, 0.95);
            PositionVennTextStack(GetSetTextNodes(textNodes, right.Id), right.X + diameter * 0.82, right.Y + diameter * 0.68, theme, 0.95);

            foreach (var overlapNode in overlapNodes)
            {
                if (!overlapNode.Metadata.TryGetValue("venn:region", out var regionObj) || regionObj is not string region)
                    continue;

                double anchorX = region switch
                {
                    "ab" => top.X + diameter * 0.33,
                    "ac" => top.X + diameter * 0.67,
                    "bc" => top.X + diameter * 0.50,
                    "abc" => top.X + diameter * 0.50,
                    _ => top.X + diameter * 0.50,
                };

                double anchorY = region switch
                {
                    "ab" => top.Y + diameter * 0.62,
                    "ac" => top.Y + diameter * 0.62,
                    "bc" => left.Y + diameter * 0.66,
                    "abc" => top.Y + diameter * 0.78,
                    _ => top.Y + diameter * 0.50,
                };

                PositionVennOverlapNode(overlapNode, anchorX, anchorY, theme);
                PositionVennTextStack(
                    GetRegionTextNodes(textNodes, region),
                    anchorX,
                    anchorY + GetNestedTextOffset(overlapNode, theme),
                    theme);
            }

            return;
        }

        double centerDistance = diameter * 0.62;
        double orbitRadius = centerDistance;
        double centerX = pad + orbitRadius + diameter / 2;
        double centerY = pad + titleOffset + orbitRadius + diameter / 2;

        for (int i = 0; i < setNodes.Count; i++)
        {
            double angle = (-Math.PI / 2) + (2 * Math.PI * i / setNodes.Count);
            double nodeCenterX = centerX + Math.Cos(angle) * orbitRadius;
            double nodeCenterY = centerY + Math.Sin(angle) * orbitRadius;
            setNodes[i].X = nodeCenterX - diameter / 2;
            setNodes[i].Y = nodeCenterY - diameter / 2;
            SetLabelCenter(setNodes[i], diameter * 0.5, diameter * 0.5);
        }
    }

    private static void LayoutMatrixDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        var cells = diagram.Nodes.Values
            .Where(node => node.Metadata.ContainsKey("matrix:row") && node.Metadata.ContainsKey("matrix:column"))
            .OrderBy(node => GetMetadataInt(node.Metadata, "matrix:row"))
            .ThenBy(node => GetMetadataInt(node.Metadata, "matrix:column"))
            .ToList();

        if (cells.Count == 0)
            return;

        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;
        double baseFontSize = cells.Max(node => node.Label.FontSize ?? theme.FontSize);
        int maxLineCount = cells.Max(node => GetTextLineCount(node.Label.Text));
        double maxTextWidth = cells.Max(node => EstimateTextWidth(node.Label.Text, node.Label.FontSize ?? theme.FontSize));

        double cellWidth = Math.Max(minW + 24, maxTextWidth + theme.NodePadding * 2.5);
        double textBlockHeight = Math.Max(1, maxLineCount) * baseFontSize * 1.15;
        double cellHeight = Math.Max(nodeH + baseFontSize * 0.7, textBlockHeight + theme.NodePadding * 2.6);
        double gap = Math.Max(theme.NodePadding, 18);

        string[] palette = theme.NodePalette is { Count: > 0 }
            ? [.. theme.NodePalette]
            : [theme.NodeFillColor, theme.NodeFillColor, theme.NodeFillColor, theme.NodeFillColor];

        foreach (var cell in cells)
        {
            int row = GetMetadataInt(cell.Metadata, "matrix:row");
            int column = GetMetadataInt(cell.Metadata, "matrix:column");
            int paletteIndex = Math.Clamp(row * 2 + column, 0, palette.Length - 1);
            string fill = palette[paletteIndex];

            cell.Shape = Shape.RoundedRectangle;
            cell.Width = cellWidth;
            cell.Height = cellHeight;
            cell.X = pad + column * (cellWidth + gap);
            cell.Y = pad + titleOffset + row * (cellHeight + gap);
            cell.FillColor = fill;
            cell.StrokeColor = theme.NodeStrokePalette is { Count: > 0 }
                ? theme.NodeStrokePalette[paletteIndex % theme.NodeStrokePalette.Count]
                : ColorUtils.Darken(fill, 0.18);
            SetLabelCenter(cell, cellWidth / 2, cellHeight / 2);
        }
    }

    private static void SetLabelCenter(Node node, double x, double y)
    {
        node.Metadata["label:centerX"] = x;
        node.Metadata["label:centerY"] = y;
    }

    private static void PositionTextOnlyNode(Node node, double anchorX, double anchorY, Theme theme)
    {
        double fontSize = node.Label.FontSize ?? theme.FontSize;
        node.X = anchorX;
        node.Y = anchorY - fontSize * 0.35;
    }

    private static IEnumerable<Node> GetSetTextNodes(IEnumerable<Node> textNodes, string setId) =>
        textNodes.Where(node => string.Equals(node.Metadata.GetValueOrDefault("venn:parentSet") as string, setId, StringComparison.Ordinal));

    private static IEnumerable<Node> GetRegionTextNodes(IEnumerable<Node> textNodes, string region) =>
        textNodes.Where(node => string.Equals(node.Metadata.GetValueOrDefault("venn:region") as string, region, StringComparison.Ordinal));

    private static void PositionVennOverlapNode(Node? node, double anchorX, double anchorY, Theme theme)
    {
        if (node is null)
            return;

        PositionTextOnlyNode(node, anchorX, anchorY, theme);
    }

    private static void PositionVennTextStack(IEnumerable<Node> nodes, double anchorX, double firstAnchorY, Theme theme)
        => PositionVennTextStack(nodes, anchorX, firstAnchorY, theme, 1.15);

    private static void PositionVennTextStack(IEnumerable<Node> nodes, double anchorX, double firstAnchorY, Theme theme, double lineSpacingMultiplier)
    {
        double currentAnchorY = firstAnchorY;
        foreach (var node in nodes)
        {
            PositionTextOnlyNode(node, anchorX, currentAnchorY, theme);
            currentAnchorY += (node.Label.FontSize ?? theme.FontSize) * lineSpacingMultiplier;
        }
    }

    private static double GetNestedTextOffset(Node? overlapNode, Theme theme) =>
        overlapNode is not null && !string.IsNullOrWhiteSpace(overlapNode.Label.Text)
            ? theme.FontSize * 1.15
            : 0;

    private static void LayoutXyChartDiagram(
        Diagram diagram,
        Theme theme,
        double pad)
    {
        // Chart dimensions.
        const double ChartWidth = 500;
        const double ChartHeight = 300;
        const double AxisLabelMarginLeft = 50;
        const double AxisLabelMarginBottom = 30;

        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;

        // Chart area origin (top-left of the plot region).
        double chartX = pad + AxisLabelMarginLeft;
        double chartY = pad + titleOffset;
        double plotWidth = ChartWidth;
        double plotHeight = ChartHeight;

        // Read axis metadata.
        int categoryCount = diagram.Metadata.TryGetValue("xychart:categoryCount", out var ccObj)
            ? Convert.ToInt32(ccObj, System.Globalization.CultureInfo.InvariantCulture) : 0;
        double yMin = diagram.Metadata.TryGetValue("xychart:yMin", out var yMinObj)
            ? Convert.ToDouble(yMinObj, System.Globalization.CultureInfo.InvariantCulture) : 0;
        double yMax = diagram.Metadata.TryGetValue("xychart:yMax", out var yMaxObj)
            ? Convert.ToDouble(yMaxObj, System.Globalization.CultureInfo.InvariantCulture) : 100;
        int barSeriesCount = diagram.Metadata.TryGetValue("xychart:barSeriesCount", out var bscObj)
            ? Convert.ToInt32(bscObj, System.Globalization.CultureInfo.InvariantCulture) : 0;

        double yRange = yMax - yMin;
        if (yRange <= 0) yRange = 1;

        double categoryWidth = categoryCount > 0 ? plotWidth / categoryCount : plotWidth;
        double barGroupWidth = categoryWidth * 0.7;
        double barWidth = barSeriesCount > 0 ? barGroupWidth / barSeriesCount : barGroupWidth;
        double barOffsetInCategory = (categoryWidth - barGroupWidth) / 2;

        // Store chart geometry for the renderer.
        diagram.Metadata["xychart:chartX"] = chartX;
        diagram.Metadata["xychart:chartY"] = chartY;
        diagram.Metadata["xychart:plotWidth"] = plotWidth;
        diagram.Metadata["xychart:plotHeight"] = plotHeight;

        foreach (var node in diagram.Nodes.Values)
        {
            if (!node.Metadata.TryGetValue("xychart:kind", out var kindObj))
                continue;

            var kind = kindObj as string;
            int ci = node.Metadata.TryGetValue("xychart:categoryIndex", out var ciObj)
                ? Convert.ToInt32(ciObj, System.Globalization.CultureInfo.InvariantCulture) : 0;
            double value = node.Metadata.TryGetValue("xychart:value", out var valObj)
                ? Convert.ToDouble(valObj, System.Globalization.CultureInfo.InvariantCulture) : 0;

            // Clamp value to axis range.
            double clampedValue = Math.Max(yMin, Math.Min(yMax, value));
            double normalized = (clampedValue - yMin) / yRange;
            double barHeight = normalized * plotHeight;

            if (kind == "bar")
            {
                int si = node.Metadata.TryGetValue("xychart:seriesIndex", out var siObj)
                    ? Convert.ToInt32(siObj, System.Globalization.CultureInfo.InvariantCulture) : 0;

                node.X = chartX + ci * categoryWidth + barOffsetInCategory + si * barWidth;
                node.Y = chartY + plotHeight - barHeight;
                node.Width = barWidth;
                node.Height = barHeight;
            }
            else if (kind == "linePoint")
            {
                double pointX = chartX + ci * categoryWidth + categoryWidth / 2;
                double pointY = chartY + plotHeight - normalized * plotHeight;
                node.X = pointX - 3;
                node.Y = pointY - 3;
                node.Width = 6;
                node.Height = 6;
            }
        }

        // Store the total canvas dimensions so the renderer can size the SVG.
        double canvasWidth = chartX + plotWidth + pad;
        double canvasHeight = chartY + plotHeight + AxisLabelMarginBottom + pad;
        diagram.Metadata["xychart:canvasWidth"] = canvasWidth;
        diagram.Metadata["xychart:canvasHeight"] = canvasHeight;
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

        return text
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n')
            .Max(line => line.Length) * fontSize * AvgGlyphAdvanceEm;
    }

    private static int GetTextLineCount(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').Length;
    }

    /// <summary>
    /// Shifts all nodes and groups so that every group's top-left corner is at least
    /// <paramref name="diagramPadding"/> units from the canvas origin, preserving the
    /// outer diagram padding even when groups would otherwise sit too close to the edge.
    /// Call this after group bounding boxes have been computed.
    /// </summary>
    private static void ShiftDiagramForGroupPadding(Diagram diagram, double diagramPadding)
    {
        if (diagram.Groups.Count == 0)
            return;

        double minGroupX = diagram.Groups.Min(g => g.X);
        double minGroupY = diagram.Groups.Min(g => g.Y);
        double shiftX = Math.Max(0, diagramPadding - minGroupX);
        double shiftY = Math.Max(0, diagramPadding - minGroupY);
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
