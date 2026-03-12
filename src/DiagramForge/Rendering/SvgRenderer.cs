using System.Text;
using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Rendering;

/// <summary>
/// Renders a laid-out <see cref="Diagram"/> to a self-contained SVG string.
/// </summary>
/// <remarks>
/// The renderer produces clean, modern SVG with rounded corners, theme-driven colours,
/// and smooth edge paths. No external libraries or browser runtime is required.
/// </remarks>
public sealed class SvgRenderer : ISvgRenderer
{
    /// <inheritdoc/>
    public string Render(Diagram diagram, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(theme);

        double width = ComputeWidth(diagram, theme);
        double height = ComputeHeight(diagram, theme);

        var sb = new StringBuilder();

        // SVG root — use F() for all numeric attributes to guarantee invariant-culture output
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{SvgRenderSupport.F(width)}" height="{SvgRenderSupport.F(height)}" viewBox="0 0 {SvgRenderSupport.F(width)} {SvgRenderSupport.F(height)}">""");

        // Definitions (arrow markers, etc.)
        SvgRenderSupport.AppendDefs(sb, theme);

        // Background
        if (!theme.TransparentBackground)
        {
            sb.AppendLine($"""  <rect width="{SvgRenderSupport.F(width)}" height="{SvgRenderSupport.F(height)}" fill="{SvgRenderSupport.Escape(theme.BackgroundColor)}" rx="{SvgRenderSupport.F(theme.BorderRadius)}" ry="{SvgRenderSupport.F(theme.BorderRadius)}"/>""");
        }

        // Title
        if (!string.IsNullOrWhiteSpace(diagram.Title))
        {
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(width / 2)}" y="{SvgRenderSupport.F(theme.DiagramPadding - 4)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(theme.TitleFontSize)}" font-weight="bold" fill="{SvgRenderSupport.Escape(theme.TitleTextColor)}">{SvgRenderSupport.Escape(diagram.Title)}</text>""");
        }

        // Groups (render behind nodes). Parents render first so nested child groups
        // sit on top of the parent fill instead of being washed out by it.
        var parentMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var groupsById = new Dictionary<string, Group>(StringComparer.Ordinal);
        foreach (var group in diagram.Groups)
        {
            if (!groupsById.TryAdd(group.Id, group))
                throw new InvalidOperationException(
                    $"Duplicate group id '{group.Id}' in diagram. Group IDs must be unique.");
            foreach (var childGroupId in group.ChildGroupIds)
            {
                if (!parentMap.TryGetValue(childGroupId, out var parents))
                {
                    parents = [];
                    parentMap[childGroupId] = parents;
                }

                parents.Add(group.Id);
            }
        }

        var depthCache = new Dictionary<string, int>(StringComparer.Ordinal);
        int GetGroupDepth(Group group, HashSet<string>? visiting = null)
        {
            if (depthCache.TryGetValue(group.Id, out var cachedDepth))
                return cachedDepth;

            visiting ??= new HashSet<string>(StringComparer.Ordinal);

            // Cycle detection: if already on the call stack, break the cycle by
            // treating this group as top-level to avoid infinite recursion.
            if (!visiting.Add(group.Id))
                return depthCache[group.Id] = 0;

            if (!parentMap.TryGetValue(group.Id, out var parentIds) || parentIds.Count == 0)
            {
                visiting.Remove(group.Id);
                return depthCache[group.Id] = 0;
            }

            var depth = 1 + parentIds
                .Where(groupsById.ContainsKey)
                .Select(parentId => groupsById[parentId])
                .Max(g => GetGroupDepth(g, visiting));

            visiting.Remove(group.Id);
            depthCache[group.Id] = depth;
            return depth;
        }

        int groupIndex = 0;
        foreach (var group in diagram.Groups.OrderBy(g => GetGroupDepth(g)).ThenBy(g => g.Id, StringComparer.Ordinal))
            SvgStructureWriter.AppendGroup(sb, group, theme, groupIndex++);

        // Sequence-diagram lifelines: dashed vertical lines below each participant box.
        if (diagram.Metadata.ContainsKey("sequence:canvasHeight"))
            SvgStructureWriter.AppendLifelines(sb, diagram, theme, height);

        // XY chart axes, grid lines, and line series.
        if (diagram.Metadata.ContainsKey("xychart:chartX"))
            SvgStructureWriter.AppendXyChartAxes(sb, diagram, theme);

        // Edges (render behind nodes)
        foreach (var edge in diagram.Edges)
        {
            if (!diagram.Nodes.TryGetValue(edge.SourceId, out var source)
                || !diagram.Nodes.TryGetValue(edge.TargetId, out var target))
                continue;

            SvgStructureWriter.AppendEdge(sb, edge, source, target, theme);
        }

        // Nodes (pass index for palette cycling)
        int nodeIndex = 0;
        foreach (var node in diagram.Nodes.Values)
            SvgNodeWriter.AppendNode(sb, node, theme, nodeIndex++);

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    // ── Dimension helpers ─────────────────────────────────────────────────────

    private static double ComputeWidth(Diagram diagram, Theme theme)
    {
        if (diagram.Nodes.Count == 0)
            return 200;

        if (diagram.Metadata.TryGetValue("xychart:canvasWidth", out var xcW))
            return Convert.ToDouble(xcW, System.Globalization.CultureInfo.InvariantCulture);

        double maxX = diagram.Nodes.Values.Max(n => n.X + n.Width);
        // Group rects extend beyond their member nodes by their own padding;
        // without this, the group's right edge is clipped at the canvas boundary.
        if (diagram.Groups.Count > 0)
            maxX = Math.Max(maxX, diagram.Groups.Max(g => g.X + g.Width));
        return maxX + theme.DiagramPadding;
    }

    private static double ComputeHeight(Diagram diagram, Theme theme)
    {
        if (diagram.Nodes.Count == 0)
            return 100;

        // Sequence diagrams extend below the participant nodes with per-message rows;
        // the layout engine stores the required height so node extents don't clip messages.
        if (diagram.Metadata.TryGetValue("sequence:canvasHeight", out var seqH))
            return Convert.ToDouble(seqH, System.Globalization.CultureInfo.InvariantCulture);

        if (diagram.Metadata.TryGetValue("xychart:canvasHeight", out var xcH))
            return Convert.ToDouble(xcH, System.Globalization.CultureInfo.InvariantCulture);

        double maxY = diagram.Nodes.Values.Max(n => n.Y + n.Height);
        if (diagram.Groups.Count > 0)
            maxY = Math.Max(maxY, diagram.Groups.Max(g => g.Y + g.Height));
        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;
        return maxY + theme.DiagramPadding + titleOffset;
    }
}
