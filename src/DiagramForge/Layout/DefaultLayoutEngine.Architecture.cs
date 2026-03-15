using DiagramForge.Models;
using DiagramForge.Rendering;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutArchitectureDiagram(
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
            if (node.Shape == Shape.Circle && string.IsNullOrEmpty(node.Label.Text))
            {
                node.Width = 20;
                node.Height = 20;
            }
            else
            {
                SizeStandardNode(node, theme, minW, nodeH);
            }
        }

        var gridCol = new Dictionary<string, int>(StringComparer.Ordinal);
        var gridRow = new Dictionary<string, int>(StringComparer.Ordinal);

        if (diagram.Nodes.Count > 0)
        {
            var firstId = diagram.Nodes.Keys.OrderBy(k => k, StringComparer.Ordinal).First();
            gridCol[firstId] = 0;
            gridRow[firstId] = 0;
        }

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

                int dColOffset = 0;
                int dRowOffset = 0;
                if ((srcPort == "R" && dstPort == "L") || (srcPort == "L" && dstPort == "R"))
                    dColOffset = srcPort == "R" ? 1 : -1;
                else if ((srcPort == "B" && dstPort == "T") || (srcPort == "T" && dstPort == "B"))
                    dRowOffset = srcPort == "B" ? 1 : -1;

                if (dColOffset == 0 && dRowOffset == 0)
                    continue;

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

        int minCol = gridCol.Values.Min();
        int minRow = gridRow.Values.Min();
        foreach (var id in gridCol.Keys.ToList())
        {
            gridCol[id] -= minCol;
            gridRow[id] -= minRow;
        }

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

        ShiftDiagramForGroupPadding(diagram, theme.DiagramPadding);
    }
}