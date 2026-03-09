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
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{F(width)}" height="{F(height)}" viewBox="0 0 {F(width)} {F(height)}">""");

        // Definitions (arrow markers, etc.)
        AppendDefs(sb, theme);

        // Background
        sb.AppendLine($"""  <rect width="{F(width)}" height="{F(height)}" fill="{Escape(theme.BackgroundColor)}" rx="{F(theme.BorderRadius)}" ry="{F(theme.BorderRadius)}"/>""");

        // Title
        if (!string.IsNullOrWhiteSpace(diagram.Title))
        {
            sb.AppendLine($"""  <text x="{F(width / 2)}" y="{F(theme.DiagramPadding - 4)}" text-anchor="middle" font-family="{Escape(theme.FontFamily)}" font-size="{F(theme.TitleFontSize)}" font-weight="bold" fill="{Escape(theme.TextColor)}">{Escape(diagram.Title)}</text>""");
        }

        // Groups (render behind nodes)
        foreach (var group in diagram.Groups)
            AppendGroup(sb, group, theme);

        // Edges (render behind nodes)
        foreach (var edge in diagram.Edges)
        {
            if (!diagram.Nodes.TryGetValue(edge.SourceId, out var source)
                || !diagram.Nodes.TryGetValue(edge.TargetId, out var target))
                continue;

            AppendEdge(sb, edge, source, target, theme);
        }

        // Nodes
        foreach (var node in diagram.Nodes.Values)
            AppendNode(sb, node, theme);

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    // ── SVG building blocks ───────────────────────────────────────────────────

    private static void AppendDefs(StringBuilder sb, Theme theme)
    {
        sb.AppendLine("  <defs>");
        sb.AppendLine($"""    <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="9" refY="3.5" orient="auto">""");
        sb.AppendLine($"""      <polygon points="0 0, 10 3.5, 0 7" fill="{Escape(theme.EdgeColor)}"/>""");
        sb.AppendLine("    </marker>");
        sb.AppendLine("  </defs>");
    }

    private static void AppendNode(StringBuilder sb, Node node, Theme theme)
    {
        string fill = Escape(node.FillColor ?? theme.NodeFillColor);
        string stroke = Escape(node.StrokeColor ?? theme.NodeStrokeColor);
        double rx = theme.BorderRadius;

        sb.AppendLine($"""  <g transform="translate({F(node.X)},{F(node.Y)})">""");

        switch (node.Shape)
        {
            case Shape.Circle:
            case Shape.Ellipse:
                double cx = node.Width / 2, cy = node.Height / 2;
                sb.AppendLine($"""    <ellipse cx="{F(cx)}" cy="{F(cy)}" rx="{F(cx)}" ry="{F(cy)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                break;

            case Shape.Diamond:
                double mx = node.Width / 2, my = node.Height / 2;
                sb.AppendLine($"""    <polygon points="{F(mx)},0 {F(node.Width)},{F(my)} {F(mx)},{F(node.Height)} 0,{F(my)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                break;

            case Shape.Pill:
            case Shape.Stadium:
                sb.AppendLine($"""    <rect width="{F(node.Width)}" height="{F(node.Height)}" rx="{F(node.Height / 2)}" ry="{F(node.Height / 2)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                break;

            default: // Rectangle / RoundedRectangle
                sb.AppendLine($"""    <rect width="{F(node.Width)}" height="{F(node.Height)}" rx="{F(rx)}" ry="{F(rx)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                break;
        }

        // Label
        double textX = node.Width / 2;
        double textY = node.Height / 2 + theme.FontSize * 0.35;
        string textColor = Escape(node.Label.Color ?? theme.TextColor);
        double fontSize = node.Label.FontSize ?? theme.FontSize;

        sb.AppendLine($"""    <text x="{F(textX)}" y="{F(textY)}" text-anchor="middle" font-family="{Escape(theme.FontFamily)}" font-size="{F(fontSize)}" fill="{textColor}">{Escape(node.Label.Text)}</text>""");
        sb.AppendLine("  </g>");
    }

    private static void AppendEdge(StringBuilder sb, Edge edge, Node source, Node target, Theme theme)
    {
        // Compute anchor points based on the dominant direction between the two nodes
        // so that horizontal layouts connect right→left and vertical layouts connect bottom→top.
        double sourceCenterX = source.X + source.Width / 2;
        double sourceCenterY = source.Y + source.Height / 2;
        double targetCenterX = target.X + target.Width / 2;
        double targetCenterY = target.Y + target.Height / 2;

        double dx = targetCenterX - sourceCenterX;
        double dy = targetCenterY - sourceCenterY;

        double x1, y1, x2, y2, cpOffset;
        string cp1, cp2;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            // Predominantly horizontal: connect right side → left side (or left → right for reversed)
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

            cpOffset = Math.Abs(x2 - x1) * 0.4;
            cp1 = $"{F(x1 + (dx >= 0 ? cpOffset : -cpOffset))},{F(y1)}";
            cp2 = $"{F(x2 - (dx >= 0 ? cpOffset : -cpOffset))},{F(y2)}";
        }
        else
        {
            // Predominantly vertical: connect bottom-center → top-center
            if (dy >= 0)
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

            cpOffset = Math.Abs(y2 - y1) * 0.4;
            cp1 = $"{F(x1)},{F(y1 + (dy >= 0 ? cpOffset : -cpOffset))}";
            cp2 = $"{F(x2)},{F(y2 - (dy >= 0 ? cpOffset : -cpOffset))}";
        }

        string strokeColor = Escape(edge.Color ?? theme.EdgeColor);
        string strokeDash = edge.LineStyle == EdgeLineStyle.Dashed ? """ stroke-dasharray="6,3" """ :
                            edge.LineStyle == EdgeLineStyle.Dotted ? """ stroke-dasharray="2,3" """ : " ";
        string markerEnd = edge.ArrowHead != ArrowHeadStyle.None ? """ marker-end="url(#arrowhead)" """ : " ";

        sb.AppendLine($"""  <path d="M {F(x1)},{F(y1)} C {cp1} {cp2} {F(x2)},{F(y2)}" fill="none" stroke="{strokeColor}" stroke-width="{F(theme.StrokeWidth)}"{strokeDash}{markerEnd}/>""");

        // Edge label
        if (edge.Label is not null && !string.IsNullOrWhiteSpace(edge.Label.Text))
        {
            double lx = (x1 + x2) / 2;
            double ly = (y1 + y2) / 2 - 4;
            sb.AppendLine($"""  <text x="{F(lx)}" y="{F(ly)}" text-anchor="middle" font-family="{Escape(theme.FontFamily)}" font-size="{F(theme.FontSize * 0.85)}" fill="{Escape(theme.SubtleTextColor)}" font-style="italic">{Escape(edge.Label.Text)}</text>""");
        }
    }

    private static void AppendGroup(StringBuilder sb, Group group, Theme theme)
    {
        string fill = Escape(group.FillColor ?? "#F3F4F6");
        string stroke = Escape(group.StrokeColor ?? "#D1D5DB");

        sb.AppendLine($"""  <rect x="{F(group.X)}" y="{F(group.Y)}" width="{F(group.Width)}" height="{F(group.Height)}" rx="{F(theme.BorderRadius)}" ry="{F(theme.BorderRadius)}" fill="{fill}" stroke="{stroke}" stroke-width="{F(theme.StrokeWidth)}" opacity="0.6"/>""");

        if (!string.IsNullOrWhiteSpace(group.Label.Text))
        {
            sb.AppendLine($"""  <text x="{F(group.X + 8)}" y="{F(group.Y + theme.FontSize + 4)}" font-family="{Escape(theme.FontFamily)}" font-size="{F(theme.FontSize * 0.9)}" fill="{Escape(theme.SubtleTextColor)}" font-weight="bold">{Escape(group.Label.Text)}</text>""");
        }
    }

    // ── Dimension helpers ─────────────────────────────────────────────────────

    private static double ComputeWidth(Diagram diagram, Theme theme)
    {
        if (diagram.Nodes.Count == 0)
            return 200;

        double maxX = diagram.Nodes.Values.Max(n => n.X + n.Width);
        return maxX + theme.DiagramPadding;
    }

    private static double ComputeHeight(Diagram diagram, Theme theme)
    {
        if (diagram.Nodes.Count == 0)
            return 100;

        double maxY = diagram.Nodes.Values.Max(n => n.Y + n.Height);
        double titleOffset = !string.IsNullOrWhiteSpace(diagram.Title) ? theme.TitleFontSize + 8 : 0;
        return maxY + theme.DiagramPadding + titleOffset;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string F(double v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string? text) =>
        text is null ? string.Empty
            : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
