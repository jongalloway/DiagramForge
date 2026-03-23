using DiagramForge.Models;
using DiagramForge.Rendering;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    // ── Wireframe layout constants ────────────────────────────────────────────

    private const double WireframeVGap = 8.0;
    private const double WireframeHGap = 8.0;
    private const double WireframeContainerPad = 12.0;
    private const double WireframeDefaultWidth = 320.0;
    private const double WireframeButtonHeight = 34.0;
    private const double WireframeButtonMinWidthRatio = 0.4;   // fraction of DefaultWidth
    private const double WireframeButtonHPad = 32.0;           // horizontal text padding in button
    private const double WireframeInputHeight = 32.0;
    private const double WireframeControlHeight = 22.0;
    private const double WireframeControlLabelGap = 8.0;       // space between control and its label
    private const double WireframeControlMinWidth = 120.0;     // minimum width for checkbox/radio
    private const double WireframeToggleWidth = 48.0;
    private const double WireframeToggleHeight = 24.0;
    private const double WireframeTabBarHeight = 38.0;
    private const double WireframeDividerHeight = 4.0;
    private const double WireframeBadgeHeight = 20.0;
    private const double WireframeBadgeMinWidth = 36.0;        // minimum badge pill width
    private const double WireframeBadgeHPad = 16.0;            // horizontal padding inside badge
    private const double WireframeImageHeight = 100.0;
    private const double WireframeImageWidth = 160.0;
    private const double WireframeH1FontSize = 20.0;
    private const double WireframeH2FontSize = 16.0;
    private const double WireframeH3FontSize = 14.0;

    // ── Dispatch ──────────────────────────────────────────────────────────────

    private static bool TryLayoutWireframeDiagram(Diagram diagram, Theme theme, double pad)
    {
        if (!string.Equals(diagram.SourceSyntax, "wireframe", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(diagram.DiagramType, "wireframe", StringComparison.OrdinalIgnoreCase))
            return false;

        LayoutWireframeDiagram(diagram, theme, pad);
        return true;
    }

    // ── Main entry point ──────────────────────────────────────────────────────

    private static void LayoutWireframeDiagram(Diagram diagram, Theme theme, double pad)
    {
        if (diagram.Nodes.Count == 0)
            return;

        if (!diagram.Nodes.TryGetValue(Parsers.Wireframe.WireframeDslParser.RootNodeId, out var root))
            return;

        // Build parent→children map from wireframe:containment edges.
        var childrenOf = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in diagram.Edges)
        {
            if (!edge.Metadata.TryGetValue("wireframe:containment", out var v) || v is not true)
                continue;

            if (!childrenOf.TryGetValue(edge.SourceId, out var children))
            {
                children = [];
                childrenOf[edge.SourceId] = children;
            }
            children.Add(edge.TargetId);
        }

        // Size leaf nodes (containers are sized bottom-up in the next step).
        foreach (var node in diagram.Nodes.Values)
            SizeWireframeLeaf(node, theme);

        // Compute container sizes bottom-up.
        ComputeWireframeSize(root.Id, diagram.Nodes, childrenOf, theme);

        // Stretch sub-containers (card/header/footer/nested column) to fill the
        // available width of their parent column — a top-down correction pass.
        StretchContainerWidths(root.Id, diagram.Nodes, childrenOf);

        // Position all nodes top-down, starting children at (pad, pad).
        PlaceWireframeSubtree(root.Id, pad, pad, diagram.Nodes, childrenOf);
    }

    // ── Width stretching ──────────────────────────────────────────────────────

    /// <summary>
    /// Top-down pass: expand container children (card/header/footer/column) to fill
    /// the full content width of their parent column. Leaf widgets keep their
    /// natural widths.
    /// </summary>
    private static void StretchContainerWidths(
        string nodeId,
        Dictionary<string, Node> nodes,
        Dictionary<string, List<string>> childrenOf)
    {
        if (!nodes.TryGetValue(nodeId, out var node))
            return;
        if (!childrenOf.TryGetValue(nodeId, out var children) || children.Count == 0)
            return;

        string kind = node.Metadata.TryGetValue("wireframe:kind", out var k) ? k as string ?? "column" : "column";
        bool isRoot = node.Metadata.ContainsKey("wireframe:isRoot");
        bool isRow = kind == "row";

        if (!isRow)
        {
            double contentWidth = isRoot ? node.Width : node.Width - WireframeContainerPad * 2;

            foreach (var childId in children)
            {
                if (!nodes.TryGetValue(childId, out var child))
                    continue;

                // Only expand containers — leaf widgets keep their natural width.
                string childKind = child.Metadata.TryGetValue("wireframe:kind", out var ck) ? ck as string ?? "text" : "text";
                bool childIsContainer = childKind is "column" or "row" or "card" or "header" or "footer";

                if (childIsContainer)
                    child.Width = contentWidth;

                StretchContainerWidths(childId, nodes, childrenOf);
            }
        }
        else
        {
            foreach (var childId in children)
                StretchContainerWidths(childId, nodes, childrenOf);
        }
    }

    // ── Leaf sizing ───────────────────────────────────────────────────────────

    private static void SizeWireframeLeaf(Node node, Theme theme)
    {
        if (!node.Metadata.TryGetValue("wireframe:kind", out var kindObj) || kindObj is not string kind)
            return;

        switch (kind)
        {
            case "button":
            {
                double textW = SvgRenderSupport.EstimateTextWidth(node.Label.Text, theme.FontSize);
                node.Width = Math.Max(WireframeDefaultWidth * WireframeButtonMinWidthRatio, textW + WireframeButtonHPad);
                node.Height = WireframeButtonHeight;
                break;
            }
            case "textinput":
                node.Width = WireframeDefaultWidth - (WireframeContainerPad * 2);
                node.Height = WireframeInputHeight;
                break;

            case "checkbox":
            case "radio":
            {
                double textW = SvgRenderSupport.EstimateTextWidth(node.Label.Text, theme.FontSize);
                node.Width = Math.Max(WireframeControlMinWidth, WireframeControlHeight + WireframeControlLabelGap + textW);
                node.Height = WireframeControlHeight;
                break;
            }
            case "toggle":
                node.Width = WireframeToggleWidth
                    + (string.IsNullOrWhiteSpace(node.Label.Text)
                        ? 0
                        : WireframeControlLabelGap + SvgRenderSupport.EstimateTextWidth(node.Label.Text, theme.FontSize));
                node.Height = WireframeToggleHeight;
                break;

            case "dropdown":
                node.Width = WireframeDefaultWidth - (WireframeContainerPad * 2);
                node.Height = WireframeInputHeight;
                break;

            case "tabs":
                node.Width = WireframeDefaultWidth;
                node.Height = WireframeTabBarHeight;
                break;

            case "badge":
            {
                double badgeFontSize = theme.FontSize * 0.8;
                double textW = SvgRenderSupport.EstimateTextWidth(node.Label.Text, badgeFontSize);
                node.Width = Math.Max(WireframeBadgeMinWidth, textW + WireframeBadgeHPad);
                node.Height = WireframeBadgeHeight;
                break;
            }
            case "image":
                node.Width = Math.Max(WireframeImageWidth, WireframeDefaultWidth * 0.6);
                node.Height = WireframeImageHeight;
                break;

            case "divider":
                node.Width = WireframeDefaultWidth;
                node.Height = WireframeDividerHeight;
                break;

            case "heading":
            {
                int level = node.Metadata.TryGetValue("wireframe:headingLevel", out var lvObj) && lvObj is int lv ? lv : 1;
                double fs = level == 1 ? WireframeH1FontSize : (level == 2 ? WireframeH2FontSize : WireframeH3FontSize);
                double textW = SvgRenderSupport.EstimateTextWidth(node.Label.Text, fs);
                node.Width = Math.Max(80, textW + 8);
                node.Height = fs * 1.6;
                node.Label.FontSize = fs;
                break;
            }
            case "text":
            {
                bool bold = node.Metadata.TryGetValue("wireframe:bold", out var bv) && bv is true;
                double fs = bold ? theme.FontSize * 1.05 : theme.FontSize;
                double textW = SvgRenderSupport.EstimateTextWidth(node.Label.Text, fs);
                node.Width = Math.Max(80, textW + 8);
                node.Height = fs * 1.6;
                break;
            }
            // Containers (column, row, card, header, footer): sized later.
        }
    }

    // ── Bottom-up container sizing ────────────────────────────────────────────

    private static void ComputeWireframeSize(
        string nodeId,
        Dictionary<string, Node> nodes,
        Dictionary<string, List<string>> childrenOf,
        Theme theme)
    {
        if (!nodes.TryGetValue(nodeId, out var node))
            return;

        if (!childrenOf.TryGetValue(nodeId, out var children) || children.Count == 0)
            return; // leaf — already sized

        // Recursively size children first.
        foreach (var childId in children)
            ComputeWireframeSize(childId, nodes, childrenOf, theme);

        string kind = node.Metadata.TryGetValue("wireframe:kind", out var k) ? k as string ?? "column" : "column";
        bool isRoot = node.Metadata.ContainsKey("wireframe:isRoot");
        bool isRow = kind == "row";

        double contentW, contentH;

        if (isRow)
        {
            double total = 0;
            double maxH = 0;
            foreach (var id in children)
            {
                if (!nodes.TryGetValue(id, out var cn)) continue;
                total += cn.Width;
                if (cn.Height > maxH) maxH = cn.Height;
            }
            total += Math.Max(0, children.Count - 1) * WireframeHGap;
            contentW = total;
            contentH = maxH;
        }
        else
        {
            double maxW = 0;
            double total = 0;
            foreach (var id in children)
            {
                if (!nodes.TryGetValue(id, out var cn)) continue;
                if (cn.Width > maxW) maxW = cn.Width;
                total += cn.Height;
            }
            total += Math.Max(0, children.Count - 1) * WireframeVGap;
            contentW = maxW;
            contentH = total;
        }

        double padH = isRoot ? 0 : WireframeContainerPad;
        double padV = isRoot ? 0 : WireframeContainerPad;

        // Allocate room for a container label (card/header/footer with non-empty label).
        double titleH = 0;
        if (!isRoot && !string.IsNullOrWhiteSpace(node.Label.Text) && kind is "card" or "header" or "footer")
        {
            titleH = theme.FontSize * 1.5 + WireframeVGap;
            node.Metadata["wireframe:titleHeight"] = titleH;
        }

        node.Width = contentW + (padH * 2);
        node.Height = contentH + (padV * 2) + titleH;
    }

    // ── Top-down positioning ──────────────────────────────────────────────────

    private static void PlaceWireframeSubtree(
        string nodeId,
        double x,
        double y,
        Dictionary<string, Node> nodes,
        Dictionary<string, List<string>> childrenOf)
    {
        if (!nodes.TryGetValue(nodeId, out var node))
            return;

        bool isRoot = node.Metadata.ContainsKey("wireframe:isRoot");

        // Place the node itself (root keeps its default 0,0 position).
        if (!isRoot)
        {
            node.X = x;
            node.Y = y;
        }

        if (!childrenOf.TryGetValue(nodeId, out var children) || children.Count == 0)
            return;

        string kind = node.Metadata.TryGetValue("wireframe:kind", out var k) ? k as string ?? "column" : "column";
        bool isRow = kind == "row";
        double pad = isRoot ? 0 : WireframeContainerPad;
        double titleH = node.Metadata.TryGetValue("wireframe:titleHeight", out var thObj) && thObj is double th ? th : 0;

        // Children start at (x or node.X) + pad, offset by title height vertically.
        double childX = (isRoot ? x : node.X) + pad;
        double childY = (isRoot ? y : node.Y) + pad + titleH;

        foreach (var childId in children)
        {
            if (!nodes.TryGetValue(childId, out var child))
                continue;

            PlaceWireframeSubtree(childId, childX, childY, nodes, childrenOf);

            if (isRow)
                childX += child.Width + WireframeHGap;
            else
                childY += child.Height + WireframeVGap;
        }
    }
}
