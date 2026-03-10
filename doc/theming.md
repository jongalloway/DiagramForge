# Theming

## 1. Overview

Every visual property of a DiagramForge diagram — colors, fonts, spacing,
corner radius — is controlled by a `Theme` object. Themes are resolved once
per render call and passed through the entire pipeline (layout engine + SVG
renderer).

---

## 2. Theme Precedence

When `DiagramRenderer.Render(text, theme)` is called, the effective theme is
resolved as:

```
diagram.Theme  ??  callerTheme  ??  Theme.Default
```

| Priority | Source | When it's set |
|----------|--------|--------------|
| 1 (highest) | `Diagram.Theme` | Parser sets it when the source text contains theme directives (future) |
| 2 | `theme` parameter | Caller passes a `Theme` to `Render(text, theme)` |
| 3 (lowest) | `Theme.Default` | Built-in defaults (always available) |

This means:

- A diagram can override the caller's theme (useful for self-contained diagram
  files that carry their own styling).
- A caller can theme all diagrams consistently by passing a shared `Theme`.
- Without any configuration, the built-in defaults produce clean, modern output.

---

## 3. `Theme` Properties

### 3.1 Palette

| Property | Default | Description |
|----------|---------|-------------|
| `PrimaryColor` | `#4F81BD` | Primary accent (available for future use). |
| `SecondaryColor` | `#70AD47` | Secondary accent. |
| `AccentColor` | `#ED7D31` | Tertiary accent. |
| `BackgroundColor` | `#FFFFFF` | Canvas background fill. |
| `NodeFillColor` | `#DAE8FC` | Default node background. |
| `NodeStrokeColor` | `#4F81BD` | Default node border. |
| `EdgeColor` | `#555555` | Default edge stroke and arrowhead fill. |
| `TextColor` | `#1F2937` | Default text color for node labels and titles. |
| `SubtleTextColor` | `#6B7280` | Muted color for edge labels and group headers. |

### 3.2 Typography

| Property | Default | Description |
|----------|---------|-------------|
| `FontFamily` | `"Segoe UI", Inter, Arial, sans-serif` | CSS font stack. The renderer writes this directly into SVG `font-family` attributes. |
| `FontSize` | `13` | Base font size in SVG user units (px equivalent). |
| `TitleFontSize` | `15` | Font size for the diagram title. |

### 3.3 Shape & Spacing

| Property | Default | Description |
|----------|---------|-------------|
| `BorderRadius` | `8` | `rx`/`ry` for rounded rectangles and the background. |
| `StrokeWidth` | `1.5` | Border thickness for nodes and groups. Thick edges use `2×`. |
| `NodePadding` | `12` | Internal space between a node's border and its label text. Also used as the side padding for group bounding boxes. |
| `DiagramPadding` | `24` | Space between the outermost elements and the SVG canvas edge. |

---

## 4. Per-Element Overrides

Individual nodes, edges, and groups can override specific theme colors:

| Element | Override properties |
|---------|-------------------|
| `Node` | `FillColor`, `StrokeColor` |
| `Edge` | `Color` |
| `Group` | `FillColor`, `StrokeColor` |
| `Label` | `FontSize`, `Color` |

When an override is `null`, the theme default is used. The renderer resolves
this with the null-coalesce pattern:

```csharp
string fill = node.FillColor ?? theme.NodeFillColor;
```

---

## 5. How Theme Properties Are Consumed

### 5.1 Layout Engine

The layout engine reads these theme properties:

| Property | Usage |
|----------|-------|
| `FontSize` | Used in text-width estimation (`charCount × fontSize × 0.6`) to size nodes. |
| `NodePadding` | Added to text width to compute node width; used as group bounding-box padding. |
| `DiagramPadding` | Sets the starting offset for the first row/column of nodes. |

### 5.2 SVG Renderer

The renderer reads all theme properties:

| Property | SVG usage |
|----------|----------|
| `BackgroundColor` | Full-canvas `<rect>` fill |
| `NodeFillColor/Stroke` | Node shape `fill`/`stroke` (unless overridden) |
| `EdgeColor` | `<path>` stroke and arrowhead `<polygon>` fill |
| `TextColor` | Node label `<text>` fill |
| `SubtleTextColor` | Edge label and group header `<text>` fill |
| `FontFamily` | All `<text>` elements' `font-family` |
| `FontSize` | Node labels, edge labels (`× 0.85`), group headers (`× 0.9`) |
| `TitleFontSize` | Title `<text>` element |
| `BorderRadius` | `rx`/`ry` on `<rect>` elements |
| `StrokeWidth` | `stroke-width` on shapes, edges, groups |
| `DiagramPadding` | Canvas size calculation (added to max extent) |

---

## 6. Creating a Custom Theme

### 6.1 Dark Theme Example

```csharp
var darkTheme = new Theme
{
    BackgroundColor = "#1E1E2E",
    NodeFillColor   = "#313244",
    NodeStrokeColor = "#89B4FA",
    EdgeColor       = "#A6ADC8",
    TextColor       = "#CDD6F4",
    SubtleTextColor = "#6C7086",
    FontFamily      = "\"JetBrains Mono\", monospace",
    BorderRadius    = 6,
};

string svg = new DiagramRenderer().Render(diagramText, darkTheme);
```

### 6.2 Presentation Theme Example

```csharp
var slideTheme = new Theme
{
    NodeFillColor   = "#E8F5E9",
    NodeStrokeColor = "#2E7D32",
    EdgeColor       = "#424242",
    TextColor       = "#212121",
    FontFamily      = "Inter, sans-serif",
    FontSize        = 16,
    TitleFontSize   = 20,
    NodePadding     = 16,
    DiagramPadding  = 32,
    BorderRadius    = 12,
    StrokeWidth     = 2,
};
```

### 6.3 Minimal / Whiteboard Theme

```csharp
var minimalTheme = new Theme
{
    BackgroundColor = "#FAFAFA",
    NodeFillColor   = "#FFFFFF",
    NodeStrokeColor = "#CCCCCC",
    EdgeColor       = "#999999",
    TextColor       = "#333333",
    SubtleTextColor = "#888888",
    BorderRadius    = 4,
    StrokeWidth     = 1,
};
```

---

## 7. Future: Theme Packs (PRD Phase 2)

The PRD roadmap calls for **theme packs** — collections of pre-built themes
shipped as part of the library. Design considerations:

- **Discoverability:** Themes could be exposed as static properties on `Theme`
  (e.g., `Theme.Dark`, `Theme.Presentation`, `Theme.Monochrome`).
- **Palette-based construction:** A future `Theme.FromPalette(primary,
  secondary, accent, background)` factory could derive all properties from a
  small set of base colors.
- **JSON/YAML serialization:** For CLI and config-file workflows, themes could
  be loaded from a file. The `Theme` class has no complex types — all
  properties are primitives — so serialization is straightforward.
- **Multi-node palettes:** Current `NodeFillColor` applies uniformly. To give
  each node a distinct color (like Mermaid's palette-based auto-coloring), the
  renderer would need to cycle through a `Theme.NodePalette` list, or parsers
  would need to assign `Node.FillColor` from a palette list.

---

## 8. Design Notes

- **The `Theme` class is intentionally flat.** No nested objects, no
  inheritance, no style cascading. Every property is a primitive with a
  sensible default. This keeps the API surface minimal and avoids CSS-like
  complexity.
- **Theme properties use CSS color strings** (`#RRGGBB` hex). The renderer
  writes them directly into SVG attributes with no parsing or conversion.
- **Font metrics are not theme-aware.** The text-width heuristic
  (`charCount × fontSize × 0.6`) uses a fixed constant regardless of
  `FontFamily`. This is a known limitation — some fonts (e.g., monospace) have
  significantly different advance widths. See
  [layout-architecture.md](layout-architecture.md) Phase 2c for improvement
  plans.
