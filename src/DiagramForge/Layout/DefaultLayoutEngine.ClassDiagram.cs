using DiagramForge.Models;

namespace DiagramForge.Layout;

public sealed partial class DefaultLayoutEngine
{
    // ── Class-diagram helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when the diagram contains inheritance or
    /// realization edges, signalling that narrower layers should be centered
    /// within the widest hierarchy band.
    /// </summary>
    private static bool ShouldCenterHierarchyBands(Diagram diagram)
        => diagram.Edges.Any(edge =>
            edge.Metadata.TryGetValue("class:relationshipType", out var relationshipType)
            && relationshipType is string relType
            && (string.Equals(relType, "inheritance", StringComparison.Ordinal)
                || string.Equals(relType, "realization", StringComparison.Ordinal)));

    /// <summary>
    /// Sizes a class-diagram node that carries compartments (attributes, methods, etc.).
    /// <para>
    /// Width is the widest content across the class name, annotations, and all compartment
    /// lines, plus horizontal padding. Height is the sum of the header section
    /// (annotations + class name with top/bottom padding) and all compartment sections
    /// (each preceded by a divider line and surrounded by compact vertical padding).
    /// </para>
    /// <para>
    /// Stores <c>label:centerY</c> and <c>class:headerHeight</c> in
    /// <see cref="Node.Metadata"/> so the renderer can position the class name label
    /// inside the header and draw dividers at the correct Y offsets.
    /// </para>
    /// </summary>
    private static void SizeClassNode(Node node, Theme theme, double minWidth, double minHeight)
    {
        double fontSize = node.Label.FontSize ?? theme.FontSize;
        double defaultAnnotationFontSize = fontSize * AnnotationFontSizeRatio;
        double pad = theme.NodePadding;
        double compPad = pad / 2; // compact vertical padding within each compartment
        double defaultLineHeight = fontSize * DefaultLabelLineHeight;

        // ── Width: max of (class name, annotations, compartment lines) + 2×padding ──
        double maxTextWidth = EstimateTextWidth(node.Label, fontSize);

        foreach (var ann in node.Annotations)
        {
            double annFontSize = ann.FontSize ?? defaultAnnotationFontSize;
            foreach (var annLine in GetLabelLines(ann))
                maxTextWidth = Math.Max(maxTextWidth, EstimateTextWidth($"\u00AB{annLine}\u00BB", annFontSize));
        }

        foreach (var compartment in node.Compartments)
        {
            foreach (var line in compartment.Lines)
            {
                double lineFontSize = line.FontSize ?? fontSize;
                maxTextWidth = Math.Max(maxTextWidth, EstimateTextWidth(line, lineFontSize));
            }
        }

        // ── Header height: top pad + annotations + class name + bottom pad ──
        double annotationsHeight = 0;
        foreach (var annotation in node.Annotations)
        {
            double annotationFontSize = annotation.FontSize ?? defaultAnnotationFontSize;
            annotationsHeight += GetTextLineCount(annotation) * annotationFontSize * DefaultLabelLineHeight;
        }

        if (node.Annotations.Count > 0)
            annotationsHeight += compPad;

        double labelHeight = GetTextBlockHeight(node.Label, fontSize);
        double headerHeight = pad + annotationsHeight + labelHeight + pad;

        // Tell the renderer where to vertically center the class name within the header.
        node.Metadata["label:centerY"] = pad + annotationsHeight + labelHeight / 2;
        node.Metadata["class:headerHeight"] = headerHeight;

        // ── Compartment heights: divider + compact pad + lines + compact pad ──
        double compartmentsHeight = 0;
        foreach (var compartment in node.Compartments)
        {
            compartmentsHeight += theme.StrokeWidth; // divider line
            double linesHeight = compartment.Lines.Count == 0
                ? defaultLineHeight
                : compartment.Lines.Sum(l => GetTextLineCount(l) * (l.FontSize ?? fontSize) * DefaultLabelLineHeight);
            compartmentsHeight += compPad + linesHeight + compPad;
        }

        node.Width = Math.Max(minWidth, maxTextWidth + 2 * pad);
        node.Height = Math.Max(minHeight, headerHeight + compartmentsHeight);
    }

    /// <summary>
    /// Returns the layering endpoints for an edge, reversing direction for
    /// inheritance and realization so that parent classes are ranked above children.
    /// </summary>
    private static (string SourceId, string TargetId) GetLayeringEndpoints(Edge edge)
    {
        if (edge.Metadata.TryGetValue("class:relationshipType", out var relationshipType)
            && relationshipType is string relType
            && (string.Equals(relType, "inheritance", StringComparison.Ordinal)
                || string.Equals(relType, "realization", StringComparison.Ordinal)))
        {
            return (edge.TargetId, edge.SourceId);
        }

        return (edge.SourceId, edge.TargetId);
    }
}
