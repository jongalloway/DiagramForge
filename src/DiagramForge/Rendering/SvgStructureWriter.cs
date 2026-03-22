using System.Text;
using DiagramForge.Models;

namespace DiagramForge.Rendering;

internal static class SvgStructureWriter
{
    internal enum EdgeRenderPass
    {
        Underlay,
        Overlay,
    }

    internal static void AppendEdge(StringBuilder sb, Edge edge, Node source, Node target, Theme theme, LayoutHints hints)
        => AppendEdge(sb, edge, source, target, theme, hints, EdgeRenderPass.Underlay);

    internal static void AppendEdge(StringBuilder sb, Edge edge, Node source, Node target, Theme theme, LayoutHints hints, EdgeRenderPass renderPass)
    {
        double sourceCenterX = source.X + source.Width / 2;
        double sourceCenterY = source.Y + source.Height / 2;
        double targetCenterX = target.X + target.Width / 2;
        double targetCenterY = target.Y + target.Height / 2;

        double dx = targetCenterX - sourceCenterX;
        double dy = targetCenterY - sourceCenterY;

        double x1;
        double y1;
        double x2;
        double y2;
        string cp1;
        string cp2;

        bool horizontalOverlap = (dx >= 0 && source.X + source.Width > target.X)
                              || (dx < 0 && source.X < target.X + target.Width);
        bool verticalOverlap = (dy >= 0 && source.Y + source.Height > target.Y)
                             || (dy < 0 && source.Y < target.Y + target.Height);

        bool preferHorizontal = PreferHorizontalForEdge(edge, hints, dx, dy);
        if (preferHorizontal && horizontalOverlap && !verticalOverlap)
            preferHorizontal = false;
        else if (!preferHorizontal && verticalOverlap && !horizontalOverlap)
            preferHorizontal = true;

        if (preferHorizontal)
        {
            if (dx >= 0)
            {
                x1 = source.X + source.Width;
                y1 = sourceCenterY;
                x2 = target.X;
                y2 = targetCenterY;
            }
            else
            {
                x1 = source.X;
                y1 = sourceCenterY;
                x2 = target.X + target.Width;
                y2 = targetCenterY;
            }
        }
        else if (dy >= 0)
        {
            x1 = sourceCenterX;
            y1 = source.Y + source.Height;
            x2 = targetCenterX;
            y2 = target.Y;
        }
        else
        {
            x1 = sourceCenterX;
            y1 = source.Y;
            x2 = targetCenterX;
            y2 = target.Y + target.Height;
        }

        if (TryGetMetadataDouble(edge.Metadata, "render:sourceAnchorX", out var sourceAnchorX))
            x1 = sourceAnchorX;
        if (TryGetMetadataDouble(edge.Metadata, "render:sourceAnchorY", out var sourceAnchorY))
            y1 = sourceAnchorY;
        if (TryGetMetadataDouble(edge.Metadata, "render:targetAnchorX", out var targetAnchorX))
            x2 = targetAnchorX;
        if (TryGetMetadataDouble(edge.Metadata, "render:targetAnchorY", out var targetAnchorY))
            y2 = targetAnchorY;

        double edgeDx = x2 - x1;
        double edgeDy = y2 - y1;
        double edgeLen = Math.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
        bool subtleTargetBezier = edge.Metadata.TryGetValue("target:subtleBezier", out var subtleBezierObj)
            && subtleBezierObj is true;
        cp1 = $"{SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)}";
        cp2 = $"{SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}";
        double cpDist = edgeLen * 0.4;
        if (edgeLen > 0)
        {
            double ux = edgeDx / edgeLen;
            double uy = edgeDy / edgeLen;

            if (subtleTargetBezier)
            {
                double handleX = Math.Clamp(Math.Abs(edgeDx) * 0.22, 20, 42);
                double cp1X = x1 + (edgeDx >= 0 ? handleX : -handleX);
                double cp2X = x2 - (edgeDx >= 0 ? handleX : -handleX);
                cp1 = $"{SvgRenderSupport.F(cp1X)},{SvgRenderSupport.F(y1)}";
                cp2 = $"{SvgRenderSupport.F(cp2X)},{SvgRenderSupport.F(y2)}";
            }
            else if (preferHorizontal)
                cp1 = $"{SvgRenderSupport.F(x1 + (dx >= 0 ? cpDist : -cpDist))},{SvgRenderSupport.F(y1)}";
            else
                cp1 = $"{SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1 + (dy >= 0 ? cpDist : -cpDist))}";

            if (!subtleTargetBezier)
                cp2 = $"{SvgRenderSupport.F(x2 - ux * cpDist)},{SvgRenderSupport.F(y2 - uy * cpDist)}";
        }

        bool isSequenceEdge = false;
        if (edge.Metadata.TryGetValue("sequence:messageY", out var msgYObj))
        {
            isSequenceEdge = true;
            double msgY = Convert.ToDouble(msgYObj, System.Globalization.CultureInfo.InvariantCulture);
            x1 = sourceCenterX;
            y1 = msgY;
            x2 = targetCenterX;
            y2 = msgY;
            double seqOffset = Math.Abs(x2 - x1) * 0.4;
            cp1 = $"{SvgRenderSupport.F(x1 + (x2 >= x1 ? seqOffset : -seqOffset))},{SvgRenderSupport.F(y1)}";
            cp2 = $"{SvgRenderSupport.F(x2 - (x2 >= x1 ? seqOffset : -seqOffset))},{SvgRenderSupport.F(y2)}";
        }

        string strokeColor = SvgRenderSupport.Escape(edge.Color ?? theme.EdgeColor);
        string strokeDash = edge.LineStyle switch
        {
            EdgeLineStyle.Dashed => """ stroke-dasharray="6,3" """,
            EdgeLineStyle.Dotted => """ stroke-dasharray="2,3" """,
            _ => " ",
        };
        double strokeWidth = edge.LineStyle == EdgeLineStyle.Thick ? theme.StrokeWidth * 2 : theme.StrokeWidth;
        string markerEnd = edge.ArrowHead switch
        {
            ArrowHeadStyle.Arrow => """ marker-end="url(#arrowhead)" """,
            ArrowHeadStyle.OpenArrow => """ marker-end="url(#arrowhead-open)" """,
            ArrowHeadStyle.Diamond => """ marker-end="url(#arrowhead-filled-diamond)" """,
            ArrowHeadStyle.Circle => """ marker-end="url(#arrowhead-open-diamond)" """,
            _ => " ",
        };
        string markerStart = edge.SourceArrowHead switch
        {
            ArrowHeadStyle.Arrow => """ marker-start="url(#arrowhead)" """,
            ArrowHeadStyle.OpenArrow => """ marker-start="url(#arrowhead-open)" """,
            ArrowHeadStyle.Diamond => """ marker-start="url(#arrowhead-filled-diamond)" """,
            ArrowHeadStyle.Circle => """ marker-start="url(#arrowhead-open-diamond)" """,
            _ => " ",
        };

        // Determine effective routing mode (per-edge override wins over diagram default).
        // Sequence edges always use Bezier to preserve their horizontal arrow style.
        EdgeRouting routing = isSequenceEdge ? EdgeRouting.Bezier : (edge.Routing ?? hints.EdgeRouting);

        string pathData;
        double labelX = (x1 + x2) / 2;
        double labelY = (y1 + y2) / 2 - 4;

        switch (routing)
        {
            case EdgeRouting.Orthogonal:
            {
                var (oPath, oLx, oLy) = BuildOrthogonalPath(x1, y1, x2, y2, preferHorizontal, hints.OrthogonalCornerRadius);
                pathData = oPath;
                labelX = oLx;
                labelY = oLy;
                break;
            }

            case EdgeRouting.Straight:
                pathData = $"M {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)} L {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}";
                break;

            default: // Bezier
                pathData = $"M {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)} C {cp1} {cp2} {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}";
                break;
        }

        if (TryBuildCycleArcPath(edge, source, target, out var cycleArcPath, out var cycleLabelX, out var cycleLabelY))
        {
            pathData = cycleArcPath;
            if (edge.Label is not null && !string.IsNullOrWhiteSpace(edge.Label.Text))
            {
                labelX = cycleLabelX;
                labelY = cycleLabelY + 4;
            }
        }

        bool outlinedTargetConnector = edge.Metadata.TryGetValue("target:outlinedConnector", out var outlinedObj)
            && outlinedObj is true;
        bool overlayEdge = edge.Metadata.TryGetValue("render:overlay", out var overlayObj)
            && overlayObj is true;
        double underlayStartLength = 0;
        double underlayEndLength = 0;
        bool splitTargetConnector = outlinedTargetConnector
            && overlayEdge
            && TryGetMetadataDouble(edge.Metadata, "target:underlayStartLength", out underlayStartLength)
            && TryGetMetadataDouble(edge.Metadata, "target:underlayEndLength", out underlayEndLength);

        string overlaySegmentAttributes = string.Empty;
        if (splitTargetConnector)
        {
            double totalDx = x2 - x1;
            double totalDy = y2 - y1;
            double totalLength = Math.Sqrt((totalDx * totalDx) + (totalDy * totalDy));
            double visibleStart = Math.Min(underlayStartLength, totalLength * 0.4);
            double visibleEnd = Math.Min(underlayEndLength, totalLength * 0.35);
            double overlayLength = totalLength - visibleStart - visibleEnd;

            if (totalLength > 0.001 && overlayLength > 8)
            {
                double startPercent = (visibleStart / totalLength) * 100;
                double middlePercent = (overlayLength / totalLength) * 100;
                double endPercent = 100 - startPercent - middlePercent;
                overlaySegmentAttributes = $" pathLength=\"100\" stroke-dasharray=\"0 {SvgRenderSupport.F(startPercent)} {SvgRenderSupport.F(middlePercent)} {SvgRenderSupport.F(endPercent)}\"";
            }
            else
            {
                splitTargetConnector = false;
            }
        }

        if (overlayEdge)
        {
            if (renderPass == EdgeRenderPass.Underlay)
            {
                double underlayStrokeWidth = GetTargetConnectorStrokeWidth(strokeWidth, theme);
                AppendConnectorPath(sb, pathData, strokeColor, strokeDash, outlinedTargetConnector: false, theme, includeMarkers: false, baseStrokeWidth: underlayStrokeWidth);
                return;
            }

            if (splitTargetConnector)
            {
                AppendConnectorPath(sb, pathData, strokeColor, strokeDash, outlinedTargetConnector, theme, includeMarkers: false, lineCap: "butt", extraAttributes: overlaySegmentAttributes);
                return;
            }
        }

        if (renderPass == EdgeRenderPass.Overlay)
            return;

        AppendConnectorPath(sb, pathData, strokeColor, strokeDash, outlinedTargetConnector, theme, includeMarkers: true, markerStart, markerEnd, strokeWidth);

        if (edge.SourceLabel is not null && !string.IsNullOrWhiteSpace(edge.SourceLabel.Text))
        {
            var (sourceLabelX, sourceLabelY) = ComputeEndLabelPosition(x1, y1, x2, y2, isSource: true);
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(sourceLabelX)}" y="{SvgRenderSupport.F(sourceLabelY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(theme.FontSize * 0.8)}" fill="{SvgRenderSupport.Escape(theme.SubtleTextColor)}">{SvgRenderSupport.Escape(edge.SourceLabel.Text)}</text>""");
        }

        if (edge.TargetLabel is not null && !string.IsNullOrWhiteSpace(edge.TargetLabel.Text))
        {
            var (targetLabelX, targetLabelY) = ComputeEndLabelPosition(x2, y2, x1, y1, isSource: false);
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(targetLabelX)}" y="{SvgRenderSupport.F(targetLabelY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(theme.FontSize * 0.8)}" fill="{SvgRenderSupport.Escape(theme.SubtleTextColor)}">{SvgRenderSupport.Escape(edge.TargetLabel.Text)}</text>""");
        }

        if (edge.Label is not null && !string.IsNullOrWhiteSpace(edge.Label.Text))
        {
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(labelX)}" y="{SvgRenderSupport.F(labelY)}" text-anchor="middle" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(theme.FontSize * 0.85)}" fill="{SvgRenderSupport.Escape(theme.SubtleTextColor)}" font-style="italic">{SvgRenderSupport.Escape(edge.Label.Text)}</text>""");
        }
    }

    private static (double X, double Y) ComputeEndLabelPosition(double x, double y, double otherX, double otherY, bool isSource)
    {
        double dx = otherX - x;
        double dy = otherY - y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 0.001)
            return (x, y - 10);

        double ux = dx / length;
        double uy = dy / length;
        double nx = -uy;
        double ny = ux;
        double alongOffset = isSource ? 14 : 20;
        double normalOffset = 10;

        return (
            x + (ux * alongOffset) + (nx * normalOffset),
            y + (uy * alongOffset) + (ny * normalOffset));
    }

    private static bool PreferHorizontalForEdge(Edge edge, LayoutHints hints, double dx, double dy)
    {
        if (IsHierarchyEdge(edge) || IsTreeEdge(edge))
        {
            return hints.Direction is LayoutDirection.LeftToRight or LayoutDirection.RightToLeft;
        }

        return Math.Abs(dx) >= Math.Abs(dy);
    }

    private static string GetTargetConnectorOutlineColor(Theme theme)
        => ColorUtils.IsLight(theme.BackgroundColor)
            ? "#FFFFFF"
            : "#0F172A";

    private static double GetTargetConnectorStrokeWidth(double strokeWidth, Theme theme)
        => Math.Max(strokeWidth + 0.9, theme.StrokeWidth * 1.75);

    private static void AppendConnectorPath(
        StringBuilder sb,
        string pathData,
        string strokeColor,
        string strokeDash,
        bool outlinedTargetConnector,
        Theme theme,
        bool includeMarkers,
        string markerStart = " ",
        string markerEnd = " ",
        double? baseStrokeWidth = null,
        string lineCap = "round",
        string extraAttributes = "")
    {
        double strokeWidth = baseStrokeWidth ?? theme.StrokeWidth;

        if (outlinedTargetConnector)
        {
            string outlineColor = SvgRenderSupport.Escape(GetTargetConnectorOutlineColor(theme));
            double connectorStrokeWidth = GetTargetConnectorStrokeWidth(strokeWidth, theme);
            double haloPerSide = Math.Max(0.55, theme.StrokeWidth * 0.45);
            double outlineWidth = connectorStrokeWidth + (haloPerSide * 2);
            string appliedMarkerStart = includeMarkers ? markerStart : " ";
            string appliedMarkerEnd = includeMarkers ? markerEnd : " ";
            string escapedLineCap = SvgRenderSupport.Escape(lineCap);
            sb.AppendLine($"""  <path d="{pathData}" fill="none" stroke="{outlineColor}" stroke-width="{SvgRenderSupport.F(outlineWidth)}" stroke-linecap="{escapedLineCap}" stroke-linejoin="round"{extraAttributes}{strokeDash}/>""");
            sb.AppendLine($"""  <path d="{pathData}" fill="none" stroke="{strokeColor}" stroke-width="{SvgRenderSupport.F(connectorStrokeWidth)}" stroke-linecap="{escapedLineCap}" stroke-linejoin="round"{extraAttributes}{strokeDash}{appliedMarkerStart}{appliedMarkerEnd}/>""");
            return;
        }

        string startMarker = includeMarkers ? markerStart : " ";
        string endMarker = includeMarkers ? markerEnd : " ";
        string lineCapAttribute = string.Equals(lineCap, "round", StringComparison.Ordinal)
            ? string.Empty
            : $" stroke-linecap=\"{SvgRenderSupport.Escape(lineCap)}\"";
        sb.AppendLine($"""  <path d="{pathData}" fill="none" stroke="{strokeColor}" stroke-width="{SvgRenderSupport.F(strokeWidth)}"{lineCapAttribute}{extraAttributes}{strokeDash}{startMarker}{endMarker}/>""");
    }

    private static bool TryGetMetadataDouble(IReadOnlyDictionary<string, object> metadata, string key, out double value)
    {
        if (metadata.TryGetValue(key, out var rawValue))
        {
            value = Convert.ToDouble(rawValue, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        value = 0;
        return false;
    }

    private static bool IsHierarchyEdge(Edge edge) =>
        edge.Metadata.TryGetValue("class:relationshipType", out var relationshipType)
        && relationshipType is string relType
        && (string.Equals(relType, "inheritance", StringComparison.Ordinal)
            || string.Equals(relType, "realization", StringComparison.Ordinal));

    private static bool IsTreeEdge(Edge edge) =>
        edge.Metadata.ContainsKey("tree:edge");

    /// <summary>
    /// Builds an orthogonal (rectilinear) SVG path with rounded corners between two anchor points.
    /// The path uses a Z-shape (two 90° bends) with SVG arcs replacing each sharp corner.
    /// </summary>
    private static (string pathData, double labelX, double labelY) BuildOrthogonalPath(
        double x1, double y1, double x2, double y2, bool preferHorizontal, double cornerRadius)
    {
        if (preferHorizontal)
        {
            // Route: (x1,y1) → (midX,y1) → (midX,y2) → (x2,y2)
            double midX = (x1 + x2) / 2;
            double labX = midX;
            double labY = (y1 + y2) / 2 - 4;

            // Degenerate: nodes are horizontally aligned (no vertical delta) or
            // vertically aligned (x2≈x1 → zero-length outer segments). Fall back to
            // a straight line in both cases to avoid zero-length segments that can
            // cause inconsistent marker-end orientation in some SVG renderers.
            if (Math.Abs(y2 - y1) < 0.5 || Math.Abs(x2 - x1) < 0.5)
                return ($"M {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)} L {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}", labX, labY);

            double seg1 = Math.Abs(midX - x1);
            double seg2 = Math.Abs(y2 - y1);
            double seg3 = Math.Abs(x2 - midX);
            // r must not exceed the length of any adjacent segment; the middle segment is
            // shared by both bends, so cap at half its length to leave room for both arcs.
            double r = Math.Min(cornerRadius, Math.Min(seg1, Math.Min(seg2 / 2.0, seg3)));

            if (r < 0.5)
            {
                return ($"M {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)} L {SvgRenderSupport.F(midX)},{SvgRenderSupport.F(y1)} L {SvgRenderSupport.F(midX)},{SvgRenderSupport.F(y2)} L {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}",
                    labX, labY);
            }

            double s1 = Math.Sign(midX - x1);   // direction of seg 1 (horizontal)
            double sv = Math.Sign(y2 - y1);      // direction of seg 2 (vertical)
            double s2 = Math.Sign(x2 - midX);   // direction of seg 3 (horizontal)

            // Bend 1 sweep: incoming (s1,0) → outgoing (0,sv). cross = s1*sv.
            int sweep1 = s1 * sv > 0 ? 1 : 0;
            // Bend 2 sweep: incoming (0,sv) → outgoing (s2,0). cross = -sv*s2.
            int sweep2 = -sv * s2 > 0 ? 1 : 0;

            return (string.Concat(
                $"M {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)} ",
                $"L {SvgRenderSupport.F(midX - s1 * r)},{SvgRenderSupport.F(y1)} ",
                $"A {SvgRenderSupport.F(r)},{SvgRenderSupport.F(r)} 0 0 {sweep1} {SvgRenderSupport.F(midX)},{SvgRenderSupport.F(y1 + sv * r)} ",
                $"L {SvgRenderSupport.F(midX)},{SvgRenderSupport.F(y2 - sv * r)} ",
                $"A {SvgRenderSupport.F(r)},{SvgRenderSupport.F(r)} 0 0 {sweep2} {SvgRenderSupport.F(midX + s2 * r)},{SvgRenderSupport.F(y2)} ",
                $"L {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}"),
                labX, labY);
        }
        else
        {
            // Route: (x1,y1) → (x1,midY) → (x2,midY) → (x2,y2)
            double midY = (y1 + y2) / 2;
            double labX = (x1 + x2) / 2;
            double labY = midY - 4;

            // Degenerate: nodes are vertically aligned (no horizontal delta) or
            // horizontally aligned (y2≈y1 → zero-length outer segments). Fall back to
            // a straight line in both cases to avoid zero-length segments that can
            // cause inconsistent marker-end orientation in some SVG renderers.
            if (Math.Abs(x2 - x1) < 0.5 || Math.Abs(y2 - y1) < 0.5)
                return ($"M {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)} L {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}", labX, labY);

            double seg1 = Math.Abs(midY - y1);
            double seg2 = Math.Abs(x2 - x1);
            double seg3 = Math.Abs(y2 - midY);
            // r must not exceed the length of any adjacent segment; the middle segment is
            // shared by both bends, so cap at half its length to leave room for both arcs.
            double r = Math.Min(cornerRadius, Math.Min(seg1, Math.Min(seg2 / 2.0, seg3)));

            if (r < 0.5)
            {
                return ($"M {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)} L {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(midY)} L {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(midY)} L {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}",
                    labX, labY);
            }

            double sv1 = Math.Sign(midY - y1);  // direction of seg 1 (vertical)
            double sh = Math.Sign(x2 - x1);     // direction of seg 2 (horizontal)
            double sv2 = Math.Sign(y2 - midY);  // direction of seg 3 (vertical)

            // Bend 1 sweep: incoming (0,sv1) → outgoing (sh,0). cross = -sv1*sh.
            int sweep1 = -sv1 * sh > 0 ? 1 : 0;
            // Bend 2 sweep: incoming (sh,0) → outgoing (0,sv2). cross = sh*sv2.
            int sweep2 = sh * sv2 > 0 ? 1 : 0;

            return (string.Concat(
                $"M {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(y1)} ",
                $"L {SvgRenderSupport.F(x1)},{SvgRenderSupport.F(midY - sv1 * r)} ",
                $"A {SvgRenderSupport.F(r)},{SvgRenderSupport.F(r)} 0 0 {sweep1} {SvgRenderSupport.F(x1 + sh * r)},{SvgRenderSupport.F(midY)} ",
                $"L {SvgRenderSupport.F(x2 - sh * r)},{SvgRenderSupport.F(midY)} ",
                $"A {SvgRenderSupport.F(r)},{SvgRenderSupport.F(r)} 0 0 {sweep2} {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(midY + sv2 * r)} ",
                $"L {SvgRenderSupport.F(x2)},{SvgRenderSupport.F(y2)}"),
                labX, labY);
        }
    }

    internal static void AppendGroup(StringBuilder sb, Group group, Theme theme, int groupIndex)
    {
        string baseFill = group.FillColor ?? theme.GroupFillColor;
        string baseStroke = group.StrokeColor ?? theme.GroupStrokeColor;
        SvgRenderSupport.AppendGradientDefs(sb, "  ", $"group-{groupIndex}", baseFill, baseStroke, theme, out string fill, out string stroke);
        SvgRenderSupport.AppendShadowFilterDefs(sb, "  ", $"group-{groupIndex}", theme, out string? shadowFilterId);

        string shadowAttribute = shadowFilterId is null ? string.Empty : $" filter=\"url(#{shadowFilterId})\"";

        sb.AppendLine($"""  <rect x="{SvgRenderSupport.F(group.X)}" y="{SvgRenderSupport.F(group.Y)}" width="{SvgRenderSupport.F(group.Width)}" height="{SvgRenderSupport.F(group.Height)}" rx="{SvgRenderSupport.F(theme.BorderRadius)}" ry="{SvgRenderSupport.F(theme.BorderRadius)}" fill="{fill}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"{shadowAttribute}/>""");

        if (!string.IsNullOrWhiteSpace(group.Label.Text))
        {
            double badgeFontSize = theme.FontSize * 0.82;
            double badgeWidth = SvgRenderSupport.EstimateTextWidth(group.Label.Text, badgeFontSize) + 18;
            double badgeHeight = badgeFontSize + 10;
            double badgeX = group.X + 10;
            double badgeY = group.Y + 10;
            string badgeFill = SvgRenderSupport.Escape(ColorUtils.Blend(theme.BackgroundColor, baseStroke, ColorUtils.IsLight(theme.BackgroundColor) ? 0.10 : 0.22));
            string badgeStroke = SvgRenderSupport.Escape(ColorUtils.Blend(baseStroke, theme.BackgroundColor, ColorUtils.IsLight(theme.BackgroundColor) ? 0.18 : 0.08));
            string badgeText = SvgRenderSupport.Escape(SvgRenderSupport.ResolveNodeTextColor(ColorUtils.Blend(baseFill, theme.BackgroundColor, 0.35), theme));

            sb.AppendLine($"""  <rect x="{SvgRenderSupport.F(badgeX)}" y="{SvgRenderSupport.F(badgeY)}" width="{SvgRenderSupport.F(badgeWidth)}" height="{SvgRenderSupport.F(badgeHeight)}" rx="{SvgRenderSupport.F(badgeHeight / 2)}" ry="{SvgRenderSupport.F(badgeHeight / 2)}" fill="{badgeFill}" stroke="{badgeStroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth * 0.8)}"/>""");
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(badgeX + 9)}" y="{SvgRenderSupport.F(badgeY + badgeHeight * 0.68)}" font-family="{SvgRenderSupport.Escape(theme.FontFamily)}" font-size="{SvgRenderSupport.F(badgeFontSize)}" fill="{badgeText}" font-weight="bold">{SvgRenderSupport.Escape(group.Label.Text)}</text>""");
        }
    }

    private static bool TryBuildCycleArcPath(Edge edge, Node source, Node target, out string pathData, out double labelX, out double labelY)
    {
        pathData = string.Empty;
        labelX = 0;
        labelY = 0;

        if (!edge.Metadata.TryGetValue("conceptual:cycleArc", out var isCycleArc) || isCycleArc is not true)
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

        if (!TryBuildArcThroughPoint(startX, startY, midX, midY, endX, endY, out pathData))
            return false;

        labelX = midX;
        labelY = midY - 4;
        return true;
    }

    private static (double X, double Y) ProjectPointToNodeBoundary(Node node, double directionX, double directionY)
    {
        double centerX = node.X + node.Width / 2;
        double centerY = node.Y + node.Height / 2;
        double dx = directionX;
        double dy = directionY;

        if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
            return (centerX, centerY);

        double halfWidth = node.Width / 2;
        double halfHeight = node.Height / 2;
        double scale = 1 / Math.Max(Math.Abs(dx) / halfWidth, Math.Abs(dy) / halfHeight);
        return (centerX + dx * scale, centerY + dy * scale);
    }

    private static bool TryBuildArcThroughPoint(
        double startX,
        double startY,
        double midX,
        double midY,
        double endX,
        double endY,
        out string pathData)
    {
        pathData = string.Empty;

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
        int sweepFlag = useForwardSweep ? 1 : 0;
        double selectedSweep = useForwardSweep ? forwardSweep : NormalizeAngle(startAngle - endAngle);
        int largeArcFlag = selectedSweep > Math.PI ? 1 : 0;

        pathData = $"M {SvgRenderSupport.F(startX)},{SvgRenderSupport.F(startY)} A {SvgRenderSupport.F(radius)},{SvgRenderSupport.F(radius)} 0 {largeArcFlag},{sweepFlag} {SvgRenderSupport.F(endX)},{SvgRenderSupport.F(endY)}";
        return true;
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

    internal static void AppendLifelines(StringBuilder sb, Diagram diagram, Theme theme, double canvasHeight)
    {
        string stroke = SvgRenderSupport.Escape(theme.EdgeColor);
        double bottomY = canvasHeight - theme.DiagramPadding;

        foreach (var node in diagram.Nodes.Values)
        {
            double cx = node.X + node.Width / 2;
            double topY = node.Y + node.Height;
            sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(cx)}" y1="{SvgRenderSupport.F(topY)}" x2="{SvgRenderSupport.F(cx)}" y2="{SvgRenderSupport.F(bottomY)}" stroke="{stroke}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}" stroke-dasharray="6,3"/>""");
        }
    }

    internal static void AppendXyChartAxes(StringBuilder sb, Diagram diagram, Theme theme)
    {
        double chartX = Convert.ToDouble(diagram.Metadata["xychart:chartX"], System.Globalization.CultureInfo.InvariantCulture);
        double chartY = Convert.ToDouble(diagram.Metadata["xychart:chartY"], System.Globalization.CultureInfo.InvariantCulture);
        double plotWidth = Convert.ToDouble(diagram.Metadata["xychart:plotWidth"], System.Globalization.CultureInfo.InvariantCulture);
        double plotHeight = Convert.ToDouble(diagram.Metadata["xychart:plotHeight"], System.Globalization.CultureInfo.InvariantCulture);
        double yMin = Convert.ToDouble(diagram.Metadata["xychart:yMin"], System.Globalization.CultureInfo.InvariantCulture);
        double yMax = Convert.ToDouble(diagram.Metadata["xychart:yMax"], System.Globalization.CultureInfo.InvariantCulture);
        int categoryCount = Convert.ToInt32(diagram.Metadata["xychart:categoryCount"], System.Globalization.CultureInfo.InvariantCulture);
        var categories = diagram.Metadata["xychart:categories"] as string[] ?? [];
        int lineSeriesCount = diagram.Metadata.TryGetValue("xychart:lineSeriesCount", out var lscObj)
            ? Convert.ToInt32(lscObj, System.Globalization.CultureInfo.InvariantCulture) : 0;

        string axisColor = SvgRenderSupport.Escape(theme.EdgeColor);
        string textColor = SvgRenderSupport.Escape(theme.SubtleTextColor);
        double fontSize = theme.FontSize * 0.85;
        string fontFamily = SvgRenderSupport.Escape(theme.FontFamily);
        double categoryWidth = categoryCount > 0 ? plotWidth / categoryCount : plotWidth;

        sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(chartX)}" y1="{SvgRenderSupport.F(chartY)}" x2="{SvgRenderSupport.F(chartX)}" y2="{SvgRenderSupport.F(chartY + plotHeight)}" stroke="{axisColor}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"/>""");
        sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(chartX)}" y1="{SvgRenderSupport.F(chartY + plotHeight)}" x2="{SvgRenderSupport.F(chartX + plotWidth)}" y2="{SvgRenderSupport.F(chartY + plotHeight)}" stroke="{axisColor}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"/>""");

        int yTickCount = 5;
        double yRange = yMax - yMin;
        for (int t = 0; t <= yTickCount; t++)
        {
            double frac = (double)t / yTickCount;
            double yPos = chartY + plotHeight - frac * plotHeight;
            double yVal = yMin + frac * yRange;
            string label = yVal.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

            sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(chartX - 4)}" y1="{SvgRenderSupport.F(yPos)}" x2="{SvgRenderSupport.F(chartX)}" y2="{SvgRenderSupport.F(yPos)}" stroke="{axisColor}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth)}"/>""");

            if (t > 0 && t < yTickCount)
                sb.AppendLine($"""  <line x1="{SvgRenderSupport.F(chartX)}" y1="{SvgRenderSupport.F(yPos)}" x2="{SvgRenderSupport.F(chartX + plotWidth)}" y2="{SvgRenderSupport.F(yPos)}" stroke="{axisColor}" stroke-width="0.8" opacity="0.55" stroke-dasharray="2,6" stroke-linecap="round"/>""");

            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(chartX - 8)}" y="{SvgRenderSupport.F(yPos + fontSize * 0.35)}" text-anchor="end" font-family="{fontFamily}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{textColor}">{SvgRenderSupport.Escape(label)}</text>""");
        }

        for (int ci = 0; ci < categoryCount && ci < categories.Length; ci++)
        {
            double labelX = chartX + ci * categoryWidth + categoryWidth / 2;
            double labelY = chartY + plotHeight + fontSize + 4;
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(labelX)}" y="{SvgRenderSupport.F(labelY)}" text-anchor="middle" font-family="{fontFamily}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{textColor}">{SvgRenderSupport.Escape(categories[ci])}</text>""");
        }

        if (diagram.Metadata.TryGetValue("xychart:yLabel", out var yLabelObj) && yLabelObj is string yLabel)
        {
            double labelX = chartX - 40;
            double labelY = chartY + plotHeight / 2;
            sb.AppendLine($"""  <text x="{SvgRenderSupport.F(labelX)}" y="{SvgRenderSupport.F(labelY)}" text-anchor="middle" font-family="{fontFamily}" font-size="{SvgRenderSupport.F(fontSize)}" fill="{textColor}" transform="rotate(-90,{SvgRenderSupport.F(labelX)},{SvgRenderSupport.F(labelY)})">{SvgRenderSupport.Escape(yLabel)}</text>""");
        }

        for (int si = 0; si < lineSeriesCount; si++)
        {
            var points = diagram.Nodes.Values
                .Where(n => n.Metadata.TryGetValue("xychart:kind", out var k) && k is "linePoint"
                         && n.Metadata.TryGetValue("xychart:seriesIndex", out var siObj)
                         && Convert.ToInt32(siObj, System.Globalization.CultureInfo.InvariantCulture) == si)
                .OrderBy(n => Convert.ToInt32(n.Metadata["xychart:categoryIndex"], System.Globalization.CultureInfo.InvariantCulture))
                .Select(n => $"{SvgRenderSupport.F(n.X + n.Width / 2)},{SvgRenderSupport.F(n.Y + n.Height / 2)}")
                .ToList();

            if (points.Count < 2)
                continue;

            int barSeriesCount = diagram.Metadata.TryGetValue("xychart:barSeriesCount", out var bscObj)
                ? Convert.ToInt32(bscObj, System.Globalization.CultureInfo.InvariantCulture) : 0;
            string lineColor = SvgRenderSupport.Escape(SvgRenderSupport.GetXyChartSeriesColor(theme, barSeriesCount + si));

            sb.AppendLine($"""  <polyline points="{string.Join(" ", points)}" fill="none" stroke="{lineColor}" stroke-width="{SvgRenderSupport.F(theme.StrokeWidth * 1.5)}" stroke-linejoin="round" stroke-linecap="round"/>""");
        }
    }

    internal static void AppendSnakePath(StringBuilder sb, Diagram diagram, Theme theme)
    {
        if (!diagram.Metadata.TryGetValue("snake:pathData", out var pathDataObj)
            || pathDataObj is not string pathData)
            return;

        double strokeWidth = diagram.Metadata.TryGetValue("snake:strokeWidth", out var swObj)
            ? Convert.ToDouble(swObj, System.Globalization.CultureInfo.InvariantCulture) : 8;

        var segmentColors = diagram.Metadata.TryGetValue("snake:segmentColors", out var scObj)
            ? scObj as List<string> : null;
        var segmentStops = diagram.Metadata.TryGetValue("snake:segmentStops", out var ssObj)
            ? ssObj as List<(double Start, double End)> : null;

        int nodeCount = diagram.Metadata.TryGetValue("snake:nodeCount", out var ncObj)
            ? Convert.ToInt32(ncObj, System.Globalization.CultureInfo.InvariantCulture) : 0;

        // Render the snake as a gradient path using a linearGradient.
        // Each circle gets a solid-color band (paired stops at the same color)
        // spanning the circle's width, with smooth transitions in the gaps.
        if (segmentColors is { Count: > 0 } && nodeCount > 1)
        {
            string gradientId = "snake-gradient";
            sb.AppendLine("  <defs>");
            sb.AppendLine($"""    <linearGradient id="{gradientId}" x1="0%" y1="0%" x2="100%" y2="0%">""");

            for (int i = 0; i < segmentColors.Count; i++)
            {
                string color = SvgRenderSupport.Escape(segmentColors[i]);
                if (segmentStops is { Count: > 0 } && i < segmentStops.Count)
                {
                    var (start, end) = segmentStops[i];
                    sb.AppendLine($"""      <stop offset="{SvgRenderSupport.F(start)}%" stop-color="{color}"/>""");
                    sb.AppendLine($"""      <stop offset="{SvgRenderSupport.F(end)}%" stop-color="{color}"/>""");
                }
                else
                {
                    double pct = segmentColors.Count == 1 ? 50 : (double)i / (segmentColors.Count - 1) * 100;
                    sb.AppendLine($"""      <stop offset="{SvgRenderSupport.F(pct)}%" stop-color="{color}"/>""");
                }
            }

            sb.AppendLine("    </linearGradient>");
            sb.AppendLine("  </defs>");

            // Outline stroke for definition against background
            sb.AppendLine($"""  <path d="{pathData}" fill="none" stroke="{SvgRenderSupport.Escape(theme.BackgroundColor)}" stroke-width="{SvgRenderSupport.F(strokeWidth + 6)}" stroke-linecap="round" stroke-linejoin="round"/>""");
            // Main gradient path — fully opaque for visual impact
            sb.AppendLine($"""  <path d="{pathData}" fill="none" stroke="url(#{gradientId})" stroke-width="{SvgRenderSupport.F(strokeWidth)}" stroke-linecap="round" stroke-linejoin="round"/>""");
        }

        // Render description text for each node
        string fontFamily = SvgRenderSupport.Escape(theme.FontFamily);
        double descFontSize = diagram.Metadata.TryGetValue("snake:descFontSize", out var dfsObj)
            ? Convert.ToDouble(dfsObj, System.Globalization.CultureInfo.InvariantCulture)
            : theme.FontSize;

        foreach (var node in diagram.Nodes.Values)
        {
            if (!node.Metadata.TryGetValue("snake:description", out var descObj) || descObj is not string desc)
                continue;
            if (!node.Metadata.TryGetValue("snake:descX", out var dxObj)
                || !node.Metadata.TryGetValue("snake:descY", out var dyObj))
                continue;

            double descX = Convert.ToDouble(dxObj, System.Globalization.CultureInfo.InvariantCulture);
            double descY = Convert.ToDouble(dyObj, System.Globalization.CultureInfo.InvariantCulture);
            bool descBelow = node.Metadata.TryGetValue("snake:descBelow", out var dbObj) && dbObj is true;
            double descMaxWidth = node.Metadata.TryGetValue("snake:descMaxWidth", out var dmwObj)
                ? Convert.ToDouble(dmwObj, System.Globalization.CultureInfo.InvariantCulture) : 120;

            string textColor = SvgRenderSupport.Escape(theme.SubtleTextColor);
            string anchor = "middle";
            double lineHeight = descFontSize * 1.3;

            // Wrap description text
            var words = desc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = WrapText(words, descFontSize, descMaxWidth);

            if (descBelow)
            {
                // Text below: baseline starts at descY
                for (int li = 0; li < lines.Count; li++)
                {
                    double ly = descY + li * lineHeight;
                    sb.AppendLine($"""  <text x="{SvgRenderSupport.F(descX)}" y="{SvgRenderSupport.F(ly)}" text-anchor="{anchor}" font-family="{fontFamily}" font-size="{SvgRenderSupport.F(descFontSize)}" fill="{textColor}">{SvgRenderSupport.Escape(lines[li])}</text>""");
                }
            }
            else
            {
                // Text above: last line aligns at descY, earlier lines go up
                for (int li = 0; li < lines.Count; li++)
                {
                    double ly = descY - (lines.Count - 1 - li) * lineHeight;
                    sb.AppendLine($"""  <text x="{SvgRenderSupport.F(descX)}" y="{SvgRenderSupport.F(ly)}" text-anchor="{anchor}" font-family="{fontFamily}" font-size="{SvgRenderSupport.F(descFontSize)}" fill="{textColor}">{SvgRenderSupport.Escape(lines[li])}</text>""");
                }
            }
        }
    }

    private static List<string> WrapText(string[] words, double fontSize, double maxWidth)
    {
        var lines = new List<string>();
        double charWidth = fontSize * 0.6; // Approximate average glyph advance
        var currentLine = new System.Text.StringBuilder();
        double currentWidth = 0;

        foreach (var word in words)
        {
            double wordWidth = word.Length * charWidth;

            // If the word itself is wider than maxWidth, hard-break it into chunks.
            if (wordWidth > maxWidth)
            {
                // Flush any pending line first.
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentWidth = 0;
                }

                int maxCharsPerLine = Math.Max(1, (int)(maxWidth / charWidth));
                var remaining = word;
                while (remaining.Length > 0)
                {
                    int take = Math.Min(maxCharsPerLine, remaining.Length);
                    lines.Add(remaining[..take]);
                    remaining = remaining[take..];
                }
                continue;
            }

            // Normal word: wrap at word boundary.
            double spaceWidth = currentLine.Length > 0 ? charWidth : 0;
            if (currentWidth + spaceWidth + wordWidth > maxWidth && currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
                currentWidth = 0;
            }

            if (currentLine.Length > 0)
            {
                currentLine.Append(' ');
                currentWidth += charWidth;
            }

            currentLine.Append(word);
            currentWidth += wordWidth;
        }

        if (currentLine.Length > 0)
            lines.Add(currentLine.ToString());

        return lines.Count > 0 ? lines : [""];
    }
}