using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
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
            double labelWidth = EstimateTextWidth(node.Label, fontSize);
            var nestedTextNodes = GetSetTextNodes(textNodes, node.Id).ToList();
            double nestedTextWidth = nestedTextNodes.Count == 0
                ? 0
                : nestedTextNodes.Max(textNode => EstimateTextWidth(textNode.Label, textNode.Label.FontSize ?? theme.FontSize));
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
}