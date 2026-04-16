# Rendering Pipeline

## 1. Overview

This document traces a diagram from raw text to final SVG, showing how
`DiagramRenderer.Render()` orchestrates the four pipeline stages. It ties
together the parser, semantic model, layout engine, and SVG renderer
documentation.

---

## 2. Entry Points

`DiagramRenderer` exposes two public overloads:

```csharp
public string Render(string diagramText);
public string Render(string diagramText, Theme? theme);
```

Both return a complete, self-contained SVG string. The first delegates to the
second with `theme: null`.

---

## 3. Pipeline Stages

### Stage 1 — Parser Selection

```csharp
var parser = FindParser(diagramText);
```

`FindParser` iterates the registered `IDiagramParser` list in order and calls
`CanParse(diagramText)` on each. The **first match** wins.

Default order:

1. `MermaidParser` — matches `flowchart`, `graph`, `mindmap`, `stateDiagram`,
   and known-but-unsupported Mermaid keywords.
2. `ConceptualDslParser` — matches `diagram: <type>`.

User-registered parsers (via `RegisterParser`) are prepended and tried first.

If no parser matches, a `DiagramParseException` is thrown with the list of
registered syntax IDs.

### Stage 2 — Parsing

```csharp
var diagram = parser.Parse(diagramText);
```

The selected parser processes the input and returns a fully populated `Diagram`:

- Nodes with IDs, labels, and shapes.
- Edges with source/target IDs, styles, and optional labels.
- Groups with child node IDs.
- `LayoutHints` (direction, spacing).
- Optionally, a diagram-level `Theme` override.

At this point, no coordinates have been assigned — `X`, `Y`, `Width`, `Height`
are all `0.0`.

For Mermaid input, this stage involves two sub-steps:

1. `MermaidDocument.Parse` — strips comments, detects diagram kind.
2. Sub-parser dispatch — `MermaidFlowchartParser`, `MermaidMindmapParser`, or
   `MermaidStateParser`.

### Stage 3 — Theme Resolution

```csharp
var effectiveTheme = diagram.Theme ?? theme ?? _defaultTheme;
```

Theme precedence (first non-null wins):

1. **Diagram-embedded theme** — set by the parser if the source text contains
   theme directives.
2. **Caller-supplied theme** — the `theme` parameter on `Render`.
3. **Default theme** — `Theme.Default` (the constructor's `_defaultTheme`).

The resolved theme is passed to both the layout engine and the renderer.

### Stage 4 — Layout

```csharp
_layoutEngine.Layout(diagram, effectiveTheme);
```

The layout engine **mutates** the diagram in place:

1. **Sizing pass** — computes `Width` and `Height` for every node based on
   label text and theme padding.
2. **Layer assignment** — BFS topological sort (Kahn's algorithm) assigns each
   node to a rank/layer.
3. **Coordinate assignment** — positions nodes in a grid based on direction
   (TB/BT/LR/RL), with optional reversal for RL/BT.
4. **Group bounding boxes** — computes `X`, `Y`, `Width`, `Height` for each
   group from its member nodes' final positions + padding.
5. **Negative-space shift** — if any group extends into negative coordinates,
   the entire diagram is shifted so nothing clips.

After this stage, every `Node` and `Group` has valid coordinates.

See [layout-architecture.md](layout-architecture.md) for full details.

### Stage 5 — SVG Rendering

```csharp
return _svgRenderer.Render(diagram, effectiveTheme);
```

The renderer is **read-only** — it does not modify the diagram. It produces SVG
in this order:

1. **`<svg>` root** — width and height computed from the maximum node/group
   extent + diagram padding.
2. **`<defs>`** — arrow markers for edge arrowheads.
3. **Background `<rect>`** — full-canvas fill with rounded corners.
4. **Title `<text>`** — centered at the top, if `Diagram.Title` is set.
5. **Subtitle `<text>`** — centered below the title in a smaller, muted font, if `Diagram.Subtitle` is set.
6. **Groups** — `<rect>` + optional `<text>` label for each group. Rendered
   first so they appear behind nodes.
7. **Edges** — cubic Bézier `<path>` elements with anchor points on node edges.
   Rendered behind nodes. Optional edge labels at the midpoint.
8. **Nodes** — `<g>` containing a shape element (`<rect>`, `<ellipse>`,
   `<polygon>`) and a centered `<text>` label.
9. **`</svg>`** close.

The output is a single self-contained SVG string — no external stylesheets, no
`<foreignObject>`, no embedded HTML. All text uses native `<text>` elements.

---

## 4. Data Flow Diagram

```
                           ┌─────────────────────┐
  diagramText ────────────►│  Parser Selection    │
                           │  (CanParse loop)     │
                           └─────────┬───────────┘
                                     │ IDiagramParser
                                     ▼
                           ┌─────────────────────┐
                           │  Parse               │
                           │  text → Diagram      │
                           └─────────┬───────────┘
                                     │ Diagram (no coords)
                                     ▼
                           ┌─────────────────────┐
  theme / _defaultTheme ──►│  Theme Resolution    │
                           └─────────┬───────────┘
                                     │ effectiveTheme
                                     ▼
                           ┌─────────────────────┐
                           │  Layout              │
                           │  mutates X/Y/W/H     │
                           └─────────┬───────────┘
                                     │ Diagram (with coords)
                                     ▼
                           ┌─────────────────────┐
                           │  SVG Renderer        │
                           │  Diagram → string    │
                           └─────────┬───────────┘
                                     │
                                     ▼
                                  SVG string
```

---

## 5. Dependency Injection

The full constructor accepts all dependencies explicitly:

```csharp
public DiagramRenderer(
    IEnumerable<IDiagramParser> parsers,
    ILayoutEngine layoutEngine,
    ISvgRenderer svgRenderer,
    Theme defaultTheme)
```

This supports:

- **Testing** — inject mock parsers, a no-op layout engine, or a stub
  renderer.
- **Custom layout** — swap `DefaultLayoutEngine` for MSAGL or another
  `ILayoutEngine` implementation.
- **Custom rendering** — replace `SvgRenderer` for an alternative output
  format (future).

The default constructor wires up the standard pipeline with no configuration
required.

---

## 6. Error Handling

| Condition | Behaviour |
|-----------|-----------|
| No parser matches | `DiagramParseException` with registered syntax IDs |
| Parser fails | `DiagramParseException` with message and optional `LineNumber` |
| Empty input | `DiagramParseException` ("null or whitespace") |
| Edge references missing node | Layout ignores; renderer skips the edge silently |
| Group references missing node | Layout computes zero-size bounding box |

The pipeline does **not** throw from the layout or rendering stages under
normal conditions — those stages operate on whatever the parser produced.
