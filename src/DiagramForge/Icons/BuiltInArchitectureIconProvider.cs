using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Icons;

/// <summary>
/// Provides the five built-in Mermaid architecture diagram icons:
/// <c>cloud</c>, <c>database</c>, <c>disk</c>, <c>internet</c>, and <c>server</c>.
/// </summary>
/// <remarks>
/// SVG paths are designed to render clearly at 24×24 viewBox, matching Mermaid's
/// built-in icon set for architecture diagrams.
/// </remarks>
internal sealed class BuiltInArchitectureIconProvider : IIconProvider
{
    private const string ViewBox = "0 0 24 24";

    private static readonly Dictionary<string, string> Icons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cloud"] =
            """<path d="M18.74 10.04A6.87 6.87 0 0 0 12 4C9.34 4 7.05 5.64 5.9 8.04A5.5 5.5 0 0 0 1 14c0 3.31 2.47 6 5.5 6h11.93c2.53 0 4.58-2.24 4.58-5 0-2.64-1.88-4.78-4.27-4.96z" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round"/>""",

        ["database"] =
            """<g fill="none" stroke="currentColor" stroke-width="1.5"><ellipse cx="12" cy="5.5" rx="8" ry="3.5"/><path d="M4 5.5v13c0 1.93 3.58 3.5 8 3.5s8-1.57 8-3.5v-13"/><path d="M4 12c0 1.93 3.58 3.5 8 3.5s8-1.57 8-3.5"/></g>""",

        ["disk"] =
            """<g fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="3"/><line x1="12" y1="2" x2="12" y2="9"/></g>""",

        ["internet"] =
            """<g fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><ellipse cx="12" cy="12" rx="4" ry="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M4.93 5h14.14M4.93 19h14.14"/></g>""",

        ["server"] =
            """<g fill="none" stroke="currentColor" stroke-width="1.5"><rect x="2" y="2" width="20" height="8" rx="2" ry="2"/><rect x="2" y="14" width="20" height="8" rx="2" ry="2"/><circle cx="6" cy="6" r="1" fill="currentColor"/><circle cx="6" cy="18" r="1" fill="currentColor"/></g>""",
    };

    public DiagramIcon? GetIcon(string name)
    {
        if (Icons.TryGetValue(name, out var svgContent))
            return new DiagramIcon("builtin", name, ViewBox, svgContent);

        return null;
    }

    public IEnumerable<string> AvailableIcons => Icons.Keys;
}
