using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DiagramForge.Icons;

/// <summary>
/// Sanitizes SVG icon content to prevent XSS and other injection attacks.
/// Only safe SVG elements and attributes are permitted.
/// </summary>
internal static class SvgIconSanitizer
{
    private static readonly HashSet<string> AllowedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg", "g", "path", "circle", "rect", "ellipse",
        "line", "polyline", "polygon", "defs", "clipPath", "mask",
    };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "d", "fill", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin",
        "transform", "viewBox", "xmlns", "width", "height",
        "cx", "cy", "r", "rx", "ry", "x", "y", "x1", "y1", "x2", "y2",
        "points", "id", "clip-path", "clip-rule", "mask",
        "fill-rule", "fill-opacity", "stroke-opacity", "opacity",
        "stroke-dasharray", "stroke-dashoffset", "stroke-miterlimit",
    };

    /// <summary>
    /// Sanitizes an SVG fragment, removing unsafe elements and attributes.
    /// Returns <see langword="null"/> if the content is entirely unsafe or cannot be parsed.
    /// </summary>
    public static string? Sanitize(string? svgContent)
    {
        if (string.IsNullOrWhiteSpace(svgContent))
            return null;

        XElement root;
        bool addedWrapper = false;
        try
        {
            // Wrap bare fragments in <svg> if needed for parsing.
            string xml = svgContent.TrimStart();
            if (!xml.StartsWith('<'))
                return null;

            // Use safe XML reader settings to prevent XXE / entity expansion attacks.
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };

            // Try parsing as-is; if that fails, wrap in a <g> for multi-element fragments.
            try
            {
                using var reader = XmlReader.Create(new System.IO.StringReader(svgContent), settings);
                root = XElement.Load(reader, LoadOptions.PreserveWhitespace);
            }
            catch (XmlException)
            {
                addedWrapper = true;
                using var reader = XmlReader.Create(new System.IO.StringReader($"<g>{svgContent}</g>"), settings);
                root = XElement.Load(reader, LoadOptions.PreserveWhitespace);
            }
        }
        catch (XmlException)
        {
            return null;
        }

        SanitizeElement(root);

        // If the root was stripped of all content, return null.
        if (!root.HasElements && !root.HasAttributes && string.IsNullOrWhiteSpace(root.Value))
            return null;

        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
            ConformanceLevel = ConformanceLevel.Fragment,
        }))
        {
            // Strip the outer <svg> wrapper to prevent nested <svg> in the rendered output
            // (DiagramIcon.SvgContent contract: no outer <svg>).
            // Also strip the internal parser-added <g> wrapper — it was not part of the
            // original content. Original <g> elements from the input are preserved as-is
            // so that attributes like fill="none" stroke="currentColor" are not lost.
            if (root.Name.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase) || addedWrapper)
            {
                foreach (var child in root.Nodes())
                    child.WriteTo(writer);
            }
            else
            {
                root.WriteTo(writer);
            }
        }

        string result = sb.ToString();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static void SanitizeElement(XElement element)
    {
        // Remove disallowed elements.
        var children = element.Elements().ToList();
        foreach (var child in children)
        {
            string localName = child.Name.LocalName;
            if (!AllowedElements.Contains(localName))
            {
                child.Remove();
            }
            else
            {
                SanitizeElement(child);
            }
        }

        // Remove disallowed attributes.
        var attributes = element.Attributes().ToList();
        foreach (var attr in attributes)
        {
            string attrName = attr.Name.LocalName;

            // Block event handlers (onclick, onload, etc.).
            if (attrName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            {
                attr.Remove();
                continue;
            }

            // Block javascript: URIs in any attribute.
            if (attr.Value.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                attr.Remove();
                continue;
            }

            // Block data: URIs (potential XSS vector).
            if (attr.Value.Contains("data:", StringComparison.OrdinalIgnoreCase)
                && !attrName.Equals("d", StringComparison.OrdinalIgnoreCase))
            {
                attr.Remove();
                continue;
            }

            // Namespace declarations are allowed.
            if (attr.IsNamespaceDeclaration)
                continue;

            if (!AllowedAttributes.Contains(attrName))
            {
                attr.Remove();
            }
        }
    }
}
