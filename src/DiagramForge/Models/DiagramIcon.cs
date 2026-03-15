namespace DiagramForge.Models;

/// <summary>
/// A resolved icon ready for rendering in SVG output.
/// </summary>
/// <param name="Pack">The icon pack namespace (e.g., "heroicons", "builtin").</param>
/// <param name="Name">The icon name within the pack (e.g., "cloud", "shield-check").</param>
/// <param name="ViewBox">The SVG viewBox string (e.g., "0 0 24 24").</param>
/// <param name="SvgContent">Sanitized SVG path/shape content (no outer &lt;svg&gt; element).</param>
public sealed record DiagramIcon(string Pack, string Name, string ViewBox, string SvgContent);
