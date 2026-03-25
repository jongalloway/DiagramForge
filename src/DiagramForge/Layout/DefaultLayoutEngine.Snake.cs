using System.Globalization;
using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    private static void LayoutSnakeDiagram(
        Diagram diagram,
        Theme theme,
        double minW,
        double nodeH,
        double pad)
    {
        var orderedNodes = diagram.Nodes.Values
            .OrderBy(n => GetMetadataInt(n.Metadata, "snake:stepIndex"))
            .ToList();

        if (orderedNodes.Count == 0)
            return;

        double fontSize = theme.FontSize;

        // Snake title uses a font size proportional to the circles (set below
        // once snakeFontSize is known).  Reserve a placeholder offset here;
        // it is updated after sizing.
        bool hasTitle = !string.IsNullOrWhiteSpace(diagram.Title);
        double snakeTitleFontSize = 0;
        double titleOffset = 0;

        // ── Wrap all multi-word labels to two lines for larger text ───────────
        foreach (var node in orderedNodes)
        {
            string text = node.Label.Text;
            if (text.Contains(' ', StringComparison.Ordinal) && !text.Contains('\n', StringComparison.Ordinal))
            {
                // Split at the space nearest the middle
                int mid = text.Length / 2;
                int bestSplit = -1;
                int bestDist = int.MaxValue;
                for (int j = 0; j < text.Length; j++)
                {
                    if (text[j] == ' ' && Math.Abs(j - mid) < bestDist)
                    {
                        bestDist = Math.Abs(j - mid);
                        bestSplit = j;
                    }
                }

                if (bestSplit > 0)
                {
                    node.Label.Text = text[..bestSplit] + "\n" + text[(bestSplit + 1)..];
                    node.Label.Lines = [text[..bestSplit], text[(bestSplit + 1)..]];
                }
            }
        }

        // ── Sizing ────────────────────────────────────────────────────────────
        // Set a generous minimum circle diameter first, then derive font size from it.
        double minCircleDiameter = fontSize * 10;
        double circleDiameter = Math.Max(minCircleDiameter, Math.Max(minW, nodeH));
        double maxIconCircleDiameter = orderedNodes
            .Select(node => EnsureIconHeight(node, circleDiameter))
            .DefaultIfEmpty(circleDiameter)
            .Max();
        circleDiameter = Math.Max(circleDiameter, maxIconCircleDiameter);

        // Derive label font size from circle diameter so text fills the circle.
        // For two-line labels the inscribed width at ~60% height is ~0.8 × diameter.
        double snakeFontSize = circleDiameter * 0.14;

        // Verify labels fit; if the widest label overflows, grow the circle
        double availableTextWidth = circleDiameter * 0.75; // inscribed text area
        double widestLine = orderedNodes.Max(node =>
        {
            string t = node.Label.Text;
            string longest = t.Contains('\n') ? t.Split('\n').MaxBy(l => l.Length)! : t;
            return EstimateTextWidth(longest, snakeFontSize);
        });
        if (widestLine > availableTextWidth)
        {
            circleDiameter *= widestLine / availableTextWidth;
            snakeFontSize = circleDiameter * 0.14;
        }

        // Store the snake-specific font size and icon size per node
        double iconSize = circleDiameter * 0.3;
        foreach (var node in orderedNodes)
        {
            node.Label.FontSize = snakeFontSize;
            node.Metadata["icon:size"] = iconSize;
            // Position icon snugly above the label text inside the circle.
            // label:centerY is at circleDiameter/2; the label then shifts down
            // by iconAreaHeight/2, so effective text top ≈ center + gap/2.
            // Place icon just above that, with a small gap.
            double iconGap = snakeFontSize * 0.15;
            double iconY = circleDiameter / 2 - iconSize - iconGap;
            node.Metadata["icon:y"] = iconY;
        }

        // Snake title size — match the circle labels so it reads well
        snakeTitleFontSize = snakeFontSize;
        titleOffset = hasTitle ? snakeTitleFontSize * 1.4 + pad : 0;

        // Description text blocks — proportional to circle size for readability
        double descFontSize = snakeFontSize * 0.85;
        double descMaxWidth = circleDiameter * 2.0;
        double descLineHeight = descFontSize * 1.35;

        // Measure max description block height
        double maxDescHeight = 0;
        foreach (var node in orderedNodes)
        {
            if (node.Metadata.TryGetValue("snake:description", out var descObj) && descObj is string desc)
            {
                int lineCount = EstimateWrappedLineCount(desc, descFontSize, descMaxWidth);
                double blockHeight = lineCount * descLineHeight;
                maxDescHeight = Math.Max(maxDescHeight, blockHeight);
            }
        }

        // Gap between circle edge and description text
        double descGap = 12;

        // ── Snake curve geometry ──────────────────────────────────────────────
        // The snake connector wraps around each circle as a semicircular arc,
        // alternating above and below. The arcs tile seamlessly — each arc's
        // endpoint is the next arc's start point, so no horizontal segments.
        double arcGap = circleDiameter * 0.25; // visual gap between arc and circle edge
        double arcRadius = circleDiameter / 2 + arcGap;
        double snakeStrokeWidth = circleDiameter * 0.22;

        // Arcs tile directly: each circle center is 2 × arcRadius apart
        double hSpacing = 2 * arcRadius;

        // Vertical space needed for descriptions that sit above/below the circles
        double descSpace = maxDescHeight > 0 ? maxDescHeight + descGap : 0;

        // Center line Y: leave room for title, top descriptions, and arc extent
        double centerY = pad + titleOffset + descSpace + arcRadius + circleDiameter / 2;

        // ── Palette ───────────────────────────────────────────────────────────
        // Use the effective palette so monochrome themes (e.g. Prism, where every
        // NodePalette entry is #FFFFFF) fall back to a chromatic palette derived
        // from the theme's BorderGradientStops or accent/secondary hue rotation.
        IReadOnlyList<string> palette = ThemePaletteResolver.ResolveEffectivePalette(theme);

        string[] strokePalette = theme.NodeStrokePalette is { Count: > 0 }
            ? [.. theme.NodeStrokePalette]
            : palette.Select(c => ColorUtils.Darken(c, 0.18)).ToArray();

        // ── Position circles ──────────────────────────────────────────────────
        // Circle centres are spaced hSpacing = 2·arcRadius apart, starting at
        // pad + arcRadius so the path's left endpoint lands exactly at x = pad
        // (avoiding negative coordinates when arcGap > 0).
        for (int i = 0; i < orderedNodes.Count; i++)
        {
            var node = orderedNodes[i];
            double cx = pad + arcRadius + i * hSpacing;
            double cy = centerY;

            node.Shape = Shape.Circle;
            node.Width = circleDiameter;
            node.Height = circleDiameter;
            node.X = cx - circleDiameter / 2;
            node.Y = cy - circleDiameter / 2;

            // Assign vibrant colors from palette
            node.FillColor = palette[i % palette.Count];
            node.StrokeColor = strokePalette[i % strokePalette.Length];

            SetLabelCenter(node, circleDiameter / 2, circleDiameter / 2);
        }

        // ── Store snake path data in diagram metadata for the renderer ────────
        // The connector is a series of tiled semicircular arcs wrapping around
        // each circle, alternating below and above. Because hSpacing = 2·arcR,
        // each arc's right endpoint is exactly the next arc's left endpoint.
        //
        // Even-indexed circles: arc wraps BELOW (sweep-flag=1 → clockwise)
        // Odd-indexed circles:  arc wraps ABOVE (sweep-flag=0 → counter-clockwise)
        var pathSegments = new List<string>();
        int n = orderedNodes.Count;

        // Start at the left edge of the first circle's arc
        double firstCx = orderedNodes[0].X + circleDiameter / 2;
        pathSegments.Add($"M {Fmt(firstCx - arcRadius)},{Fmt(centerY)}");

        for (int i = 0; i < n; i++)
        {
            double cx = orderedNodes[i].X + circleDiameter / 2;

            // Semicircular arc around circle i — arcs tile with no gap
            int sweep = (i % 2 == 0) ? 1 : 0;
            pathSegments.Add(
                $"A {Fmt(arcRadius)} {Fmt(arcRadius)} 0 0 {sweep} {Fmt(cx + arcRadius)},{Fmt(centerY)}");
        }

        string snakePathData = string.Join(" ", pathSegments);

        // Build per-segment color list and paired stop positions for the gradient.
        // Each circle gets a solid-color band equal to the circle's width, with
        // smooth gradient transitions only in the gaps between circles.
        //
        //   Path spans 2·n·arcRadius.  Circle i centre = (2i+1)·arcRadius.
        //   Solid band edges: centre ± circleRadius.
        //   ratio r = circleRadius / arcRadius  (≈ 0.667 for default arcGap).
        //   solidStart% = ((2i+1) − r) / (2n) × 100
        //   solidEnd%   = ((2i+1) + r) / (2n) × 100
        double circleRadius = circleDiameter / 2;
        double r = circleRadius / arcRadius;
        var segmentColors = new List<string>();
        var segmentStops = new List<(double Start, double End)>();
        for (int i = 0; i < n; i++)
        {
            segmentColors.Add(palette[i % palette.Count]);
            double solidStart = ((2.0 * i + 1) - r) / (2.0 * n) * 100;
            double solidEnd   = ((2.0 * i + 1) + r) / (2.0 * n) * 100;
            segmentStops.Add((solidStart, solidEnd));
        }

        diagram.Metadata["snake:pathData"] = snakePathData;
        diagram.Metadata["snake:strokeWidth"] = snakeStrokeWidth;
        diagram.Metadata["snake:segmentColors"] = segmentColors;
        diagram.Metadata["snake:segmentStops"] = segmentStops;
        diagram.Metadata["snake:nodeCount"] = n;
        diagram.Metadata["snake:descFontSize"] = descFontSize;
        if (hasTitle)
        {
            diagram.Metadata["diagram:titleFontSize"] = snakeTitleFontSize;
            diagram.Metadata["diagram:titleY"] = pad + snakeTitleFontSize;
        }

        // ── Store description text positions ──────────────────────────────────
        for (int i = 0; i < orderedNodes.Count; i++)
        {
            var node = orderedNodes[i];
            if (!node.Metadata.TryGetValue("snake:description", out var descObj) || descObj is not string desc)
                continue;

            double cx = node.X + circleDiameter / 2;
            double nodeCy = node.Y + circleDiameter / 2;

            // Alternate: even indices arc below → description below; odd arc above → desc above
            bool descBelow = i % 2 == 0;
            double descY = descBelow
                ? nodeCy + circleDiameter / 2 + arcGap + descGap
                : nodeCy - circleDiameter / 2 - arcGap - descGap;

            node.Metadata["snake:descX"] = cx;
            node.Metadata["snake:descY"] = descY;
            node.Metadata["snake:descBelow"] = descBelow;
            node.Metadata["snake:descMaxWidth"] = descMaxWidth;
        }

        // ── Set canvas dimensions to account for all elements ─────────────────
        // Width: path spans from pad (left endpoint) to pad + 2·n·arcRadius (right endpoint);
        // add one pad on each side so the stroke caps are never clipped.
        double lastCx = pad + arcRadius + (n - 1) * hSpacing;
        double canvasWidth = lastCx + arcRadius + pad;
        double canvasBottom = centerY + circleDiameter / 2 + arcRadius + descSpace + pad;
        double totalHeight = canvasBottom + pad;

        // Store so ComputeWidth / ComputeHeight can use the full extent
        diagram.Metadata["snake:canvasWidth"] = canvasWidth;
        diagram.Metadata["snake:canvasHeight"] = totalHeight;
    }

    private static int EstimateWrappedLineCount(string text, double fontSize, double maxWidth)
    {
        double charWidth = fontSize * AvgGlyphAdvanceEm;
        int charsPerLine = Math.Max(1, (int)(maxWidth / charWidth));
        int lineCount = (int)Math.Ceiling((double)text.Length / charsPerLine);
        return Math.Max(1, lineCount);
    }

    private static string Fmt(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
