using DiagramForge.Abstractions;
using DiagramForge.Layout;
using DiagramForge.Models;
using DiagramForge.Parsers.Conceptual;
using DiagramForge.Parsers.Mermaid;
using DiagramForge.Rendering;

namespace DiagramForge;

/// <summary>
/// Primary entry point for converting diagram source text to SVG.
/// </summary>
/// <remarks>
/// <para>
/// <c>DiagramRenderer</c> wires together the parser registry, layout engine, and SVG renderer.
/// Callers supply raw diagram text; the renderer returns a complete SVG string.
/// </para>
/// <para>Usage example:</para>
/// <code>
/// var renderer = new DiagramRenderer();
/// string svg = renderer.Render("flowchart LR\n  A --> B");
/// </code>
/// </remarks>
public sealed class DiagramRenderer
{
    private readonly List<IDiagramParser> _parsers;
    private readonly ILayoutEngine _layoutEngine;
    private readonly ISvgRenderer _svgRenderer;
    private readonly Theme _defaultTheme;

    /// <summary>
    /// Creates a <see cref="DiagramRenderer"/> with the default parser set, layout engine, and theme.
    /// </summary>
    public DiagramRenderer()
        : this(
            parsers: [new MermaidParser(), new ConceptualDslParser()],
            layoutEngine: new DefaultLayoutEngine(),
            svgRenderer: new SvgRenderer(),
            defaultTheme: Theme.Default)
    {
    }

    /// <summary>
    /// Creates a <see cref="DiagramRenderer"/> with explicit dependencies (for testing / DI).
    /// </summary>
    public DiagramRenderer(
        IEnumerable<IDiagramParser> parsers,
        ILayoutEngine layoutEngine,
        ISvgRenderer svgRenderer,
        Theme defaultTheme)
    {
        _parsers = [.. parsers];
        _layoutEngine = layoutEngine;
        _svgRenderer = svgRenderer;
        _defaultTheme = defaultTheme;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts <paramref name="diagramText"/> to an SVG string using the default theme.
    /// The correct parser is selected automatically based on the diagram text.
    /// </summary>
    /// <param name="diagramText">Raw diagram source text (e.g., Mermaid or Conceptual DSL).</param>
    /// <returns>A complete, self-contained SVG string.</returns>
    /// <exception cref="DiagramParseException">No registered parser can handle the input.</exception>
    public string Render(string diagramText) =>
        Render(diagramText, theme: null);

    /// <summary>
    /// Converts <paramref name="diagramText"/> to an SVG string using the supplied <paramref name="theme"/>.
    /// </summary>
    public string Render(string diagramText, Theme? theme)
    {
        var parser = FindParser(diagramText)
            ?? throw new DiagramParseException(
                $"No registered parser can handle the supplied diagram text. " +
                $"Registered syntaxes: {string.Join(", ", _parsers.Select(p => p.SyntaxId))}");

        var diagram = parser.Parse(diagramText);
        var effectiveTheme = diagram.Theme ?? theme ?? _defaultTheme;

        _layoutEngine.Layout(diagram, effectiveTheme);
        return _svgRenderer.Render(diagram, effectiveTheme);
    }

    /// <summary>
    /// Registers an additional parser. The parser is tried before the built-in parsers.
    /// </summary>
    public DiagramRenderer RegisterParser(IDiagramParser parser)
    {
        _parsers.Insert(0, parser);
        return this;
    }

    /// <summary>
    /// Returns all registered parser syntax IDs.
    /// </summary>
    public IReadOnlyList<string> RegisteredSyntaxes =>
        _parsers.Select(p => p.SyntaxId).ToList().AsReadOnly();

    // ── Private helpers ───────────────────────────────────────────────────────

    private IDiagramParser? FindParser(string diagramText)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(diagramText))
                return parser;
        }
        return null;
    }
}
