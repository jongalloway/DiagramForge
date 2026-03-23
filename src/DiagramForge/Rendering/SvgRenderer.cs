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

        // Title — diagram-type layouts may override font size / position via metadata.
        // Prefer the namespaced "diagram:titleFontSize" / "diagram:titleY" keys; fall back
        // to the legacy un-namespaced keys for backward compatibility.
        if (!string.IsNullOrWhiteSpace(diagram.Title))
        {
            double titleFontSize;
            if (diagram.Metadata.TryGetValue("diagram:titleFontSize", out var tfsObj) ||
                diagram.Metadata.TryGetValue("titleFontSize", out tfsObj))
                titleFontSize = Convert.ToDouble(tfsObj, System.Globalization.CultureInfo.InvariantCulture);
            else
                titleFontSize = theme.TitleFontSize;

            double titleY;
            if (diagram.Metadata.TryGetValue("diagram:titleY", out var tyObj) ||
                diagram.Metadata.TryGetValue("titleY", out tyObj))
                titleY = Convert.ToDouble(tyObj, System.Globalization.CultureInfo.InvariantCulture);
            else
                titleY = theme.DiagramPadding - 4;
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(width / 2)}" y="{SvgRenderSupport.F(titleY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(titleFontSize)}" font-weight="bold" fill="{SvgRenderSupport.Escape(theme.TitleTextColor)}">{SvgRenderSupport.Escape(diagram.Title)}</text>""");
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

        // Snake timeline: sinusoidal connector path drawn behind nodes.
        if (diagram.Metadata.ContainsKey("snake:pathData"))
            SvgStructureWriter.AppendSnakePath(sb, diagram, theme);

        // Edges: base pass renders normal connectors and any under-node endpoint segments
        // needed by diagram-specific overlay connectors.
        foreach (var edge in diagram.Edges)
        {
            // Wireframe containment edges are layout-only; they must not produce visible SVG.
            if (edge.Metadata.TryGetValue("wireframe:containment", out var wfc) && wfc is true)
                continue;

            if (!diagram.Nodes.TryGetValue(edge.SourceId, out var source)
                || !diagram.Nodes.TryGetValue(edge.TargetId, out var target))
                continue;

            SvgStructureWriter.AppendEdge(sb, edge, source, target, theme, diagram.LayoutHints);
        }

        // Nodes (pass index for palette cycling)
        int nodeIndex = 0;
        foreach (var node in diagram.Nodes.Values)
            SvgNodeWriter.AppendNode(sb, node, theme, nodeIndex++);

        // Diagram-specific overlay edges (for example target connectors that must stay visible
        // while crossing over concentric rings but tuck under their endpoints) render after nodes.
        foreach (var edge in diagram.Edges)
        {
            // Wireframe containment edges are layout-only; they must not produce visible SVG.
            if (edge.Metadata.TryGetValue("wireframe:containment", out var wfc2) && wfc2 is true)
                continue;

            if (!diagram.Nodes.TryGetValue(edge.SourceId, out var source)
                || !diagram.Nodes.TryGetValue(edge.TargetId, out var target))
                continue;

            SvgStructureWriter.AppendEdge(sb, edge, source, target, theme, diagram.LayoutHints, SvgStructureWriter.EdgeRenderPass.Overlay);
        }

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

        if (diagram.Metadata.TryGetValue("snake:canvasWidth", out var snakeW))
            return Convert.ToDouble(snakeW, System.Globalization.CultureInfo.InvariantCulture);

        double maxX = diagram.Nodes.Values.Max(n => n.X + n.Width);
        // Group rects extend beyond their member nodes by their own padding;
        // without this, the group's right edge is clipped at the canvas boundary.
        if (diagram.Groups.Count > 0)
            maxX = Math.Max(maxX, diagram.Groups.Max(g => g.X + g.Width));
        if (TryGetCycleArcBounds(diagram, out var cycleMinX, out var cycleMaxX, out _, out _))
            maxX = Math.Max(maxX, cycleMaxX);
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

        if (diagram.Metadata.TryGetValue("snake:canvasHeight", out var snakeH))
            return Convert.ToDouble(snakeH, System.Globalization.CultureInfo.InvariantCulture);

        double maxY = diagram.Nodes.Values.Max(n => n.Y + n.Height);
        if (diagram.Groups.Count > 0)
            maxY = Math.Max(maxY, diagram.Groups.Max(g => g.Y + g.Height));
        if (TryGetCycleArcBounds(diagram, out _, out _, out _, out var cycleMaxY))
            maxY = Math.Max(maxY, cycleMaxY);
        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;
        return maxY + theme.DiagramPadding + titleOffset;
    }

    private static bool TryGetCycleArcBounds(Diagram diagram, out double minX, out double maxX, out double minY, out double maxY)
    {
        minX = double.PositiveInfinity;
        maxX = double.NegativeInfinity;
        minY = double.PositiveInfinity;
        maxY = double.NegativeInfinity;

        bool found = false;

        foreach (var edge in diagram.Edges)
        {
            if (!TryGetCycleArcBounds(edge, diagram, out var edgeMinX, out var edgeMaxX, out var edgeMinY, out var edgeMaxY))
                continue;

            found = true;
            minX = Math.Min(minX, edgeMinX);
            maxX = Math.Max(maxX, edgeMaxX);
            minY = Math.Min(minY, edgeMinY);
            maxY = Math.Max(maxY, edgeMaxY);
        }

        return found;
    }

    private static bool TryGetCycleArcBounds(Edge edge, Diagram diagram, out double minX, out double maxX, out double minY, out double maxY)
    {
        minX = maxX = minY = maxY = 0;

        if (!edge.Metadata.TryGetValue("conceptual:cycleArc", out var isCycleArc) || isCycleArc is not true)
            return false;

        if (!diagram.Nodes.TryGetValue(edge.SourceId, out var source)
            || !diagram.Nodes.TryGetValue(edge.TargetId, out var target))
            return false;

        if (!edge.Metadata.TryGetValue("cycle:centerX", out var centerXObj)
            || !edge.Metadata.TryGetValue("cycle:centerY", out var centerYObj)
            || !edge.Metadata.TryGetValue("cycle:radius", out var radiusObj))
            return false;

        double centerX = Convert.ToDouble(centerXObj, System.Globalization.CultureInfo.InvariantCulture);
        double centerY = Convert.ToDouble(centerYObj, System.Globalization.CultureInfo.InvariantCulture);
        double radius = Convert.ToDouble(radiusObj, System.Globalization.CultureInfo.InvariantCulture);

        double sourceCenterX = source.X + source.Width / 2;
        double sourceCenterY = source.Y + source.Height / 2;
        double targetCenterX = target.X + target.Width / 2;
        double targetCenterY = target.Y + target.Height / 2;

        double sourceAngle = Math.Atan2(sourceCenterY - centerY, sourceCenterX - centerX);
        double targetAngle = Math.Atan2(targetCenterY - centerY, targetCenterX - centerX);
        double sweepAngle = NormalizeAngle(targetAngle - sourceAngle);
        if (sweepAngle <= 0)
            return false;

        double midAngle = sourceAngle + sweepAngle / 2;
        double midX = centerX + radius * Math.Cos(midAngle);
        double midY = centerY + radius * Math.Sin(midAngle);

        double startTangentX = -Math.Sin(sourceAngle);
        double startTangentY = Math.Cos(sourceAngle);
        double endTangentX = Math.Sin(targetAngle);
        double endTangentY = -Math.Cos(targetAngle);

        var (startX, startY) = ProjectPointToNodeBoundary(source, startTangentX, startTangentY);
        var (endX, endY) = ProjectPointToNodeBoundary(target, endTangentX, endTangentY);

        if (!TryGetArcBounds(startX, startY, midX, midY, endX, endY, out minX, out maxX, out minY, out maxY))
            return false;

        const double markerAllowance = 10.0;
        maxX += markerAllowance;
        maxY += markerAllowance;
        minX -= markerAllowance;
        minY -= markerAllowance;

        return true;
    }

    private static (double X, double Y) ProjectPointToNodeBoundary(Node node, double directionX, double directionY)
    {
        double centerX = node.X + node.Width / 2;
        double centerY = node.Y + node.Height / 2;

        if (Math.Abs(directionX) < double.Epsilon && Math.Abs(directionY) < double.Epsilon)
            return (centerX, centerY);

        double halfWidth = node.Width / 2;
        double halfHeight = node.Height / 2;
        double scale = 1 / Math.Max(Math.Abs(directionX) / halfWidth, Math.Abs(directionY) / halfHeight);
        return (centerX + directionX * scale, centerY + directionY * scale);
    }

    private static bool TryGetArcBounds(double startX, double startY, double midX, double midY, double endX, double endY, out double minX, out double maxX, out double minY, out double maxY)
    {
        minX = maxX = minY = maxY = 0;

        double determinant = 2 * (
            startX * (midY - endY)
            + midX * (endY - startY)
            + endX * (startY - midY));

        if (Math.Abs(determinant) < 0.001)
            return false;

        double startSquared = startX * startX + startY * startY;
        double midSquared = midX * midX + midY * midY;
        double endSquared = endX * endX + endY * endY;

        double centerX = (
            startSquared * (midY - endY)
            + midSquared * (endY - startY)
            + endSquared * (startY - midY)) / determinant;
        double centerY = (
            startSquared * (endX - midX)
            + midSquared * (startX - endX)
            + endSquared * (midX - startX)) / determinant;

        double radius = Math.Sqrt(Math.Pow(startX - centerX, 2) + Math.Pow(startY - centerY, 2));
        if (radius <= 0)
            return false;

        double startAngle = Math.Atan2(startY - centerY, startX - centerX);
        double middleAngle = Math.Atan2(midY - centerY, midX - centerX);
        double endAngle = Math.Atan2(endY - centerY, endX - centerX);

        double forwardSweep = NormalizeAngle(endAngle - startAngle);
        double middleSweep = NormalizeAngle(middleAngle - startAngle);
        bool useForwardSweep = middleSweep <= forwardSweep;
        double selectedSweep = useForwardSweep ? forwardSweep : NormalizeAngle(startAngle - endAngle);

        var points = new List<(double X, double Y)>
        {
            (startX, startY),
            (midX, midY),
            (endX, endY),
        };

        foreach (double cardinal in new[] { 0d, Math.PI / 2, Math.PI, 3 * Math.PI / 2 })
        {
            if (IsAngleOnArc(cardinal, startAngle, selectedSweep, useForwardSweep))
                points.Add((centerX + radius * Math.Cos(cardinal), centerY + radius * Math.Sin(cardinal)));
        }

        minX = points.Min(point => point.X);
        maxX = points.Max(point => point.X);
        minY = points.Min(point => point.Y);
        maxY = points.Max(point => point.Y);
        return true;
    }

    private static bool IsAngleOnArc(double angle, double startAngle, double sweep, bool useForwardSweep)
    {
        const double epsilon = 0.0001;
        double delta = useForwardSweep
            ? NormalizeAngle(angle - startAngle)
            : NormalizeAngle(startAngle - angle);

        return delta <= sweep + epsilon;
    }

    private static double NormalizeAngle(double angle)
    {
        const double Tau = Math.PI * 2;
        while (angle <= 0)
            angle += Tau;
        while (angle > Tau)
            angle -= Tau;
        return angle;
    }
}
