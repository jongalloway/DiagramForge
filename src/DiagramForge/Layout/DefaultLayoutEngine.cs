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
public sealed partial class DefaultLayoutEngine : ILayoutEngine
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
    private const double DefaultLabelLineHeight = 1.15;

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

        PrepareNodeLabels(diagram, theme);

        if (TryLayoutMermaidDiagram(diagram, theme, minW, nodeH, hGap, vGap, pad))
            return;

        if (TryLayoutConceptualDiagram(diagram, theme, minW, nodeH, pad))
        {
            return;
        }

        // ── Sizing pass ───────────────────────────────────────────────────────
        // Compute each node's width from its label so text does not overflow the
        // shape. MinNodeWidth remains a floor so short labels ("A", "End") do not
        // produce skinny boxes.

        foreach (var node in diagram.Nodes.Values)
            SizeStandardNode(node, theme, minW, nodeH);

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
                double runY = pad;
                foreach (var node in layer)
                {
                    node.X = columnX;
                    node.Y = runY;
                    runY += node.Height + vGap;
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
                    continue;

                double runX = pad;
                double rowHeight = layer.Max(node => node.Height);
                foreach (var node in layer)
                {
                    node.X = runX;
                    node.Y = rowY + (rowHeight - node.Height) / 2;
                    runX += node.Width + hGap;
                }
                rowY += rowHeight + vGap;
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

    private static void SetLabelCenter(Node node, double x, double y)
    {
        node.Metadata["label:centerX"] = x;
        node.Metadata["label:centerY"] = y;
    }

    // ── Text measurement ──────────────────────────────────────────────────────

    /// <summary>
    /// Estimates the rendered width of <paramref name="text"/> using a char-count
    /// heuristic: <c>length × fontSize × 0.6</c>. Server-side SVG has no DOM to
    /// query for <c>getBBox()</c>; this gets us within ±10% for Latin text, which
    /// is sufficient for layout (padding absorbs the slop).
    /// </summary>
    private static void PrepareNodeLabels(Diagram diagram, Theme theme)
    {
        foreach (var node in diagram.Nodes.Values)
            PrepareLabelLines(node.Label, theme, diagram.LayoutHints);
    }

    private static void PrepareLabelLines(Label label, Theme theme, LayoutHints hints)
    {
        double fontSize = label.FontSize ?? theme.FontSize;
        double maxTextWidth = hints.MaxNodeWidth.HasValue
            ? Math.Max(1, hints.MaxNodeWidth.Value - 2 * theme.NodePadding)
            : double.PositiveInfinity;

        label.Lines = WrapLabelText(label.Text, fontSize, maxTextWidth);
    }

    private static string[] WrapLabelText(string? text, double fontSize, double maxTextWidth)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            if (double.IsPositiveInfinity(maxTextWidth) || EstimateTextWidth(paragraph, fontSize) <= maxTextWidth)
            {
                lines.Add(paragraph);
                continue;
            }

            WrapParagraph(paragraph, fontSize, maxTextWidth, lines);
        }

        return [.. lines];
    }

    private static void WrapParagraph(string paragraph, double fontSize, double maxTextWidth, List<string> lines)
    {
        string currentLine = string.Empty;

        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (EstimateTextWidth(candidate, fontSize) <= maxTextWidth)
            {
                currentLine = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = string.Empty;
            }

            if (EstimateTextWidth(word, fontSize) <= maxTextWidth)
            {
                currentLine = word;
                continue;
            }

            foreach (var chunk in BreakWord(word, fontSize, maxTextWidth))
            {
                if (string.IsNullOrEmpty(currentLine))
                {
                    currentLine = chunk;
                }
                else
                {
                    lines.Add(currentLine);
                    currentLine = chunk;
                }
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);
    }

    private static IEnumerable<string> BreakWord(string word, double fontSize, double maxTextWidth)
    {
        if (string.IsNullOrEmpty(word))
            yield break;

        var chunk = new System.Text.StringBuilder();
        foreach (var ch in word)
        {
            string candidate = chunk.ToString() + ch;
            if (chunk.Length > 0 && EstimateTextWidth(candidate, fontSize) > maxTextWidth)
            {
                yield return chunk.ToString();
                chunk.Clear();
            }

            chunk.Append(ch);
        }

        if (chunk.Length > 0)
            yield return chunk.ToString();
    }

    private static void SizeStandardNode(Node node, Theme theme, double minWidth, double minHeight)
    {
        double fontSize = node.Label.FontSize ?? theme.FontSize;
        double textWidth = EstimateTextWidth(node.Label, fontSize);
        double textBlockHeight = GetTextBlockHeight(node.Label, fontSize);

        node.Width = Math.Max(minWidth, textWidth + 2 * theme.NodePadding);
        node.Height = Math.Max(minHeight, textBlockHeight + 2 * theme.NodePadding);
    }

    private static double EstimateTextWidth(Label label, double fontSize) =>
        EstimateTextWidth(string.Join('\n', GetLabelLines(label)), fontSize);

    private static double EstimateTextWidth(string? text, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return text
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n')
            .Max(line => line.Length) * fontSize * AvgGlyphAdvanceEm;
    }

    private static int GetTextLineCount(Label label) => GetLabelLines(label).Length;

    private static double GetTextBlockHeight(Label label, double fontSize)
    {
        int lineCount = GetTextLineCount(label);
        if (lineCount == 0)
            return 0;

        return fontSize + (lineCount - 1) * fontSize * DefaultLabelLineHeight;
    }

    private static string[] GetLabelLines(Label label) =>
        label.Lines is { Length: > 0 }
            ? label.Lines
            : label.Text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');

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
