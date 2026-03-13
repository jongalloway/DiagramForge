using DiagramForge.Models;
using DiagramForge.Parsers.Mermaid;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    // Inner padding added around child nodes inside a composite block group.
    private const double BlockGroupInnerPad = 8;

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
            bool isArrow = node.Shape is Shape.ArrowRight or Shape.ArrowLeft or Shape.ArrowUp or Shape.ArrowDown;
            SizeStandardNode(node, theme, isArrow ? minW * 0.75 : minW, isArrow ? nodeH + 8 : nodeH);
        }

        // ── Step 1: layout composite groups ──────────────────────────────────────
        // Build a lookup of groupId → Group for quick access.
        var groupById = diagram.Groups.Count > 0
            ? diagram.Groups.ToDictionary(g => g.Id, StringComparer.Ordinal)
            : new Dictionary<string, Group>(StringComparer.Ordinal);

        // Layout each composite group's inner nodes and compute group dimensions.
        foreach (var group in diagram.Groups)
        {
            LayoutCompositeBlockGroup(diagram, group, minW, nodeH, hGap, vGap);
        }

        // ── Step 2: collect outer-level items (top-level nodes + groups) ─────────
        // Top-level nodes are those without a "block:groupId" metadata key.
        var outerNodes = diagram.Nodes.Values
            .Where(n => n.Metadata.ContainsKey("block:row")
                     && n.Metadata.ContainsKey("block:column")
                     && !n.Metadata.ContainsKey("block:groupId"))
            .ToList();

        // Derive a combined set of outer "items" (nodes and groups) for sizing.
        // Groups participate with their computed Width and their outer-grid span.
        bool hasOuterItems = outerNodes.Count > 0 || diagram.Groups.Count > 0;
        if (!hasOuterItems)
            return;

        // Determine the outer row count from all outer-level items.
        int outerRowCount = 0;
        foreach (var n in outerNodes)
            outerRowCount = Math.Max(outerRowCount, GetMetadataInt(n.Metadata, "block:row") + 1);
        foreach (var g in diagram.Groups)
        {
            int gRow = TryGetGroupMetadataInt(diagram, g.Id, "row", out var r) ? r : 0;
            outerRowCount = Math.Max(outerRowCount, gRow + 1);
        }

        if (outerRowCount == 0)
            return;

        // Base column width: max of (top-level node widths divided by their span)
        // and (group widths divided by their outer span).
        double baseColumnWidth = minW;
        foreach (var n in outerNodes)
        {
            int span = TryGetMetadataInt(n.Metadata, "block:span", out var sv) ? sv : 1;
            baseColumnWidth = Math.Max(baseColumnWidth, n.Width / Math.Max(1, span));
        }
        foreach (var g in diagram.Groups)
        {
            int span = TryGetGroupMetadataInt(diagram, g.Id, "span", out var sv) ? sv : 1;
            baseColumnWidth = Math.Max(baseColumnWidth, g.Width / Math.Max(1, span));
        }

        // Outer row heights.
        var outerRowHeights = Enumerable.Repeat(nodeH, outerRowCount).ToArray();
        foreach (var n in outerNodes)
        {
            int row = GetMetadataInt(n.Metadata, "block:row");
            outerRowHeights[row] = Math.Max(outerRowHeights[row], n.Height);
        }
        foreach (var g in diagram.Groups)
        {
            int row = TryGetGroupMetadataInt(diagram, g.Id, "row", out var r) ? r : 0;
            outerRowHeights[row] = Math.Max(outerRowHeights[row], g.Height);
        }

        // Outer row Y offsets.
        var outerRowStarts = new double[outerRowCount];
        double currentY = pad;
        for (int row = 0; row < outerRowCount; row++)
        {
            outerRowStarts[row] = currentY;
            currentY += outerRowHeights[row] + vGap;
        }

        // ── Step 3: place outer-level nodes ──────────────────────────────────────
        foreach (var node in outerNodes)
        {
            int row = GetMetadataInt(node.Metadata, "block:row");
            int column = GetMetadataInt(node.Metadata, "block:column");
            int span = TryGetMetadataInt(node.Metadata, "block:span", out var spanValue) ? spanValue : 1;

            double slotX = pad + column * (baseColumnWidth + hGap);
            double slotWidth = baseColumnWidth * span + hGap * Math.Max(0, span - 1);
            node.X = slotX;
            node.Y = outerRowStarts[row] + (outerRowHeights[row] - node.Height) / 2;
            node.Width = Math.Max(node.Width, slotWidth);
        }

        // ── Step 4: place groups in outer grid & shift their inner nodes ─────────
        foreach (var group in diagram.Groups)
        {
            int row = TryGetGroupMetadataInt(diagram, group.Id, "row", out var r) ? r : 0;
            int column = TryGetGroupMetadataInt(diagram, group.Id, "column", out var c) ? c : 0;
            int span = TryGetGroupMetadataInt(diagram, group.Id, "span", out var s) ? s : 1;

            double slotX = pad + column * (baseColumnWidth + hGap);
            double slotWidth = baseColumnWidth * span + hGap * Math.Max(0, span - 1);

            group.X = slotX;
            group.Y = outerRowStarts[row];
            group.Width = Math.Max(group.Width, slotWidth);
            // Height was already computed in LayoutCompositeBlockGroup.

            // Shift inner-node positions by the group's absolute origin.
            double innerOriginX = group.X + BlockGroupInnerPad;
            double innerOriginY = group.Y + BlockGroupInnerPad;

            foreach (var childId in group.ChildNodeIds)
            {
                if (diagram.Nodes.TryGetValue(childId, out var childNode))
                {
                    childNode.X += innerOriginX;
                    childNode.Y += innerOriginY;
                }
            }
        }
    }

    // Lays out the inner nodes of a single composite block group at (0,0) and
    // computes group.Width and group.Height from the resulting extents.
    private static void LayoutCompositeBlockGroup(
        Diagram diagram,
        Group group,
        double minW,
        double nodeH,
        double hGap,
        double vGap)
    {
        var innerNodes = group.ChildNodeIds
            .Where(diagram.Nodes.ContainsKey)
            .Select(id => diagram.Nodes[id])
            .ToList();

        if (innerNodes.Count == 0)
        {
            group.Width = minW + 2 * BlockGroupInnerPad;
            group.Height = nodeH + 2 * BlockGroupInnerPad;
            return;
        }

        int innerRowCount = innerNodes.Max(n => GetMetadataInt(n.Metadata, "block:row")) + 1;
        double innerBaseColumnWidth = Math.Max(
            minW,
            innerNodes.Max(n =>
            {
                int span = TryGetMetadataInt(n.Metadata, "block:span", out var sv) ? sv : 1;
                return n.Width / Math.Max(1, span);
            }));

        var innerRowHeights = Enumerable.Repeat(nodeH, innerRowCount).ToArray();
        foreach (var n in innerNodes)
        {
            int row = GetMetadataInt(n.Metadata, "block:row");
            innerRowHeights[row] = Math.Max(innerRowHeights[row], n.Height);
        }

        var innerRowStarts = new double[innerRowCount];
        double runY = 0;
        for (int row = 0; row < innerRowCount; row++)
        {
            innerRowStarts[row] = runY;
            runY += innerRowHeights[row] + vGap;
        }

        // Position inner nodes relative to (0, 0) – shifted to absolute later.
        foreach (var n in innerNodes)
        {
            int row = GetMetadataInt(n.Metadata, "block:row");
            int column = GetMetadataInt(n.Metadata, "block:column");
            int span = TryGetMetadataInt(n.Metadata, "block:span", out var sv) ? sv : 1;

            double slotWidth = innerBaseColumnWidth * span + hGap * Math.Max(0, span - 1);
            n.X = column * (innerBaseColumnWidth + hGap);
            n.Y = innerRowStarts[row] + (innerRowHeights[row] - n.Height) / 2;
            n.Width = Math.Max(n.Width, slotWidth);
        }

        // Group dimensions = inner extents + padding on all sides.
        int innerColCount = TryGetGroupMetadataInt(diagram, group.Id, "columnCount", out var cc) ? cc : 1;
        double contentWidth = innerBaseColumnWidth * innerColCount + hGap * Math.Max(0, innerColCount - 1);
        double contentHeight = runY - vGap; // runY advanced one extra vGap past last row

        group.Width = contentWidth + 2 * BlockGroupInnerPad;
        group.Height = contentHeight + 2 * BlockGroupInnerPad;
    }

    // Reads an int stored in diagram.Metadata under the composite-block group key
    // "block:group:{id}:{suffix}".
    private static bool TryGetGroupMetadataInt(Diagram diagram, string groupId, string suffix, out int value)
    {
        var key = $"{MermaidBlockParser.GroupMetaPrefix}{groupId}:{suffix}";
        if (diagram.Metadata.TryGetValue(key, out var raw))
        {
            value = Convert.ToInt32(raw);
            return true;
        }
        value = 0;
        return false;
    }
}