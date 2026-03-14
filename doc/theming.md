# Theming

## 1. Overview

Every visual property of a DiagramForge diagram — colors, fonts, spacing,
corner radius, gradients, shadows, palettes, and background transparency — is
controlled by a `Theme` object. Themes are resolved once per render call and
passed through the entire pipeline (layout engine + SVG renderer).

---

## 2. Theme Precedence

When `DiagramRenderer.Render(text, theme, paletteJson, transparentBackgroundOverride)` is called, the base theme is resolved as:

```text
frontmatter.Theme  ??  diagram.Theme  ??  callerTheme  ??  Theme.Default
```

| Priority | Source | When it's set |
| -------- | ------ | ------------ |
| 1 (highest) | Frontmatter `theme` | A diagram file starts with a frontmatter block; see [frontmatter.md](frontmatter.md) |
| 2 | `Diagram.Theme` | A parser assigns a theme while building the semantic model |
| 3 | `theme` parameter | Caller passes a `Theme` to `Render(...)` |
| 4 (lowest) | `Theme.Default` | Built-in defaults |

After the base theme is chosen:

- `paletteJson` overrides the theme's `NodePalette`.
- `transparentBackgroundOverride` overrides `Theme.TransparentBackground`.
- Frontmatter `borderStyle`, `fillStyle`, and `shadowStyle` mutate the resolved theme.

This means a diagram can carry its own preferred look, while callers can still override palette and transparency for a specific render.

---

## 3. Built-in Themes

DiagramForge ships with 23 built-in theme presets:

`default`, `zinc-light`, `zinc-dark`, `dark`, `neutral`, `forest`, `presentation`, `prism`, `angled-light`, `angled-dark`, `github-light`, `github-dark`, `nord`, `nord-light`, `dracula`, `tokyo-night`, `tokyo-night-storm`, `tokyo-night-light`, `catppuccin-latte`, `catppuccin-mocha`, `solarized-light`, `solarized-dark`, `one-dark`

Resolve a preset by name with `Theme.GetByName(string?)`.

---

## 4. `Theme` Properties

### 4.1 Palette collections

| Property | Type | Description |
| -------- | ---- | ----------- |
| `NodePalette` | `List<string>?` | Fill colors cycled across nodes when individual nodes do not set `FillColor`. |
| `NodeStrokePalette` | `List<string>?` | Optional matching stroke colors for `NodePalette`. |
| `BorderGradientStops` | `List<string>?` | Explicit border gradient stops used when `UseBorderGradients` is enabled. |

### 4.2 Core colors and effects

| Property | Default | Description |
| -------- | ------- | ----------- |
| `PrimaryColor` | `#4F81BD` | Main accent color used by preset and palette derivation helpers. |
| `SecondaryColor` | `#70AD47` | Secondary accent color. |
| `AccentColor` | `#ED7D31` | Tertiary accent color. |
| `BackgroundColor` | `#FFFFFF` | Canvas background fill. |
| `SurfaceColor` | `#F6F8FB` | Base surface color used by presets for cards and fills. |
| `BorderColor` | `#6B7A90` | Semantic border color used by derived themes. |
| `NodeFillColor` | `#DAE8FC` | Default node background. |
| `NodeStrokeColor` | `#4F81BD` | Default node border. |
| `GroupFillColor` | `#F3F4F6` | Default group/subgraph background. |
| `GroupStrokeColor` | `#D1D5DB` | Default group/subgraph border. |
| `EdgeColor` | `#555555` | Default edge stroke and arrowhead fill. |
| `TextColor` | `#1F2937` | Default text color for node labels. |
| `TitleTextColor` | `#0F172A` | Diagram title color. |
| `SubtleTextColor` | `#6B7280` | Muted color for edge labels and group headers. |
| `FillStyle` | `null` | Optional semantic fill mode such as `flat`, `subtle`, or `diagonal-strong`. |
| `ShadowStyle` | `null` | Optional semantic shadow mode such as `none` or `soft`. |
| `UseGradients` | `false` | Enables gradient fills. |
| `UseBorderGradients` | `false` | Enables gradient strokes for node/group borders. |
| `GradientStrength` | `0.12` | Controls how strong gradient modulation appears. |
| `TransparentBackground` | `false` | Omits the full-canvas background rect when `true`. |
| `UseNodeShadows` | `false` | Enables node shadow rendering when supported by the SVG writer. |
| `ShadowColor` | `#0F172A` | Shadow tint. |
| `ShadowOpacity` | `0.14` | Shadow opacity. |
| `ShadowBlur` | `1.50` | Shadow blur radius. |
| `ShadowOffsetX` | `0` | Shadow x offset. |
| `ShadowOffsetY` | `1.40` | Shadow y offset. |

### 4.3 Typography

| Property | Default | Description |
| -------- | ------- | ----------- |
| `FontFamily` | `"Segoe UI", Inter, Arial, sans-serif` | CSS font stack. The renderer writes this directly into SVG `font-family` attributes. |
| `FontSize` | `13` | Base font size in SVG user units (px equivalent). |
| `TitleFontSize` | `15` | Font size for the diagram title. |

### 4.4 Shape and spacing

| Property | Default | Description |
| -------- | ------- | ----------- |
| `BorderRadius` | `8` | `rx`/`ry` for rounded rectangles and the background. |
| `StrokeWidth` | `1.5` | Border thickness for nodes and groups. Thick edges use `2×`. |
| `NodePadding` | `12` | Internal space between a node's border and its label text. Also used as the side padding for group bounding boxes. |
| `DiagramPadding` | `24` | Space between the outermost elements and the SVG canvas edge. |

---

## 5. Per-element overrides

Individual nodes, edges, and groups can override specific theme colors:

| Element | Override properties |
| ------- | ------------------- |
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

## 6. JSON, palette, and preset workflows

DiagramForge supports several theme entry points:

- `Theme.GetByName(...)` for built-in presets.
- `Theme.FromColors(...)` and `Theme.FromPalette(...)` for semantic or palette-derived themes.
- `Theme.ToJson()` and `Theme.FromJson(...)` for persistence and CLI theme files.
- CLI `--theme`, `--palette`, and `--theme-file` options.
- Diagram frontmatter for embedded theme and style directives.

Example JSON round-trip:

```csharp
var theme = Theme.GetByName("github-dark") ?? Theme.Default;
string json = theme.ToJson();
Theme reloaded = Theme.FromJson(json);
```

---

## 7. Semantic derivation model

`Theme.FromColors(...)` accepts a small set of base colors and derives all remaining theme properties automatically. You can supply as few as two parameters (`backgroundColor` and `foregroundColor`), and every other slot is derived from those.

### 7.1 Derivation precedence

When `FromColors(...)` runs, each slot is filled using the first applicable rule:

| Slot | Derivation rule |
| ---- | --------------- |
| `AccentColor` | Provided or falls back to a blue accent chosen for the background polarity (light → `#2563EB`, dark → `#60A5FA`). |
| `MutedColor` | Provided or derived by blending `foregroundColor` toward `backgroundColor` (42 % blend for light, 36 % for dark). |
| `SurfaceColor` | Provided or derived by blending `backgroundColor` toward `accentColor` (10 % for light, 18 % for dark). |
| `BorderColor` | Provided or derived by blending `accentColor` toward `backgroundColor` (22 % for light, 12 % for dark). |
| `LineColor` | Provided or derived by blending `foregroundColor` toward `backgroundColor` (36 % for light, 30 % for dark). |
| `NodeFillColor` | Set to `surfaceColor`. |
| `NodeStrokeColor` | Set to `borderColor`. |
| `GroupFillColor` | Derived as a lightly tinted, semi-transparent blend of `backgroundColor` and `surfaceColor`. |
| `GroupStrokeColor` | Derived by blending `borderColor` toward `backgroundColor`. |
| `EdgeColor` | Set to `lineColor`. |
| `TextColor` / `TitleTextColor` | Set directly to `foregroundColor` — the caller must supply a color with sufficient contrast. |
| `SubtleTextColor` | Set to `mutedColor`. |
| `NodePalette` | Six-entry list generated by blending `surfaceColor` toward `accentColor` and `foregroundColor` at increasing intensities. |
| `NodeStrokePalette` | Derived by darkening each `NodePalette` entry (24 % for light backgrounds, 18 % for dark). |

### 7.2 `FromPalette` vs `FromColors`

`Theme.FromPalette(primaryColor, ...)` is a higher-level helper that builds a full color vocabulary from an accent color and optional background, then internally calls `FromColors(...)`. Use `FromColors(...)` when you already have explicit background and foreground colors in mind; use `FromPalette(...)` when you have only an accent or brand color.

### 7.3 Accessibility notes

DiagramForge built-in themes are designed so that `TextColor` has a contrast ratio of at least 3.0:1 against both `NodeFillColor` and `BackgroundColor`. This is a DiagramForge-specific readability target for diagram labels — it is stricter than the 1:1 baseline but is intentionally lower than the WCAG AA thresholds (4.5:1 for normal text at or below ~18 px, 3.0:1 for "large text" defined as ≥18 pt / ≈24 px or ≥14 pt bold / ≈18.7 px). DiagramForge's default `FontSize` of 13 px falls below the WCAG "large text" cutoff, so callers targeting strict WCAG AA compliance should verify that their custom themes produce a 4.5:1 ratio.

`ColorUtils.GetContrastRatio(hex1, hex2)` is available for custom theme validation:

```csharp
double ratio = ColorUtils.GetContrastRatio(myTheme.TextColor, myTheme.NodeFillColor);
// DiagramForge readability target: ≥ 3.0:1
// WCAG AA normal text (13 px): ≥ 4.5:1
// WCAG AA large text (≥24 px or ≥18.7 px bold): ≥ 3.0:1
```

---

## 8. How theme properties are consumed

### 8.1 Layout engine

The layout engine reads these theme properties:

| Property | Usage |
| -------- | ----- |
| `FontSize` | Used in text-width estimation (`charCount × fontSize × 0.6`) to size nodes. |
| `NodePadding` | Added to text width to compute node width; used as group bounding-box padding. |
| `DiagramPadding` | Sets the starting offset for the first row/column of nodes. |

### 8.2 SVG renderer

The renderer reads all theme properties, including gradients, palettes, title color, group styling, transparency, and shadow settings.

| Property | SVG usage |
| -------- | --------- |
| `BackgroundColor` | Full-canvas `<rect>` fill unless `TransparentBackground` is `true` |
| `NodeFillColor` / `NodePalette` | Node `fill` selection when not overridden per node |
| `NodeStrokeColor` / `NodeStrokePalette` | Node `stroke` selection when not overridden per node |
| `GroupFillColor` / `GroupStrokeColor` | Group container `fill` / `stroke` |
| `EdgeColor` | `<path>` stroke and arrowhead `<polygon>` fill |
| `TextColor` | Node label `<text>` fill |
| `TitleTextColor` | Diagram title `<text>` fill |
| `SubtleTextColor` | Edge label and group header `<text>` fill |
| `FontFamily` | All `<text>` elements' `font-family` |
| `FontSize` | Node labels, edge labels (`× 0.85`), group headers (`× 0.9`) |
| `TitleFontSize` | Title `<text>` element |
| `BorderRadius` | `rx`/`ry` on `<rect>` elements |
| `StrokeWidth` | `stroke-width` on shapes, edges, groups |
| `UseGradients`, `UseBorderGradients`, `GradientStrength`, `BorderGradientStops` | Gradient defs and fill/stroke paint selection |
| `ShadowStyle`, `UseNodeShadows`, `ShadowColor`, `ShadowOpacity`, `ShadowBlur`, `ShadowOffsetX`, `ShadowOffsetY` | SVG filter generation and shadow application |
| `DiagramPadding` | Canvas size calculation (added to max extent) |

---

## 9. Creating a custom theme

### 9.1 Dark theme example

```csharp
var darkTheme = new Theme
{
    BackgroundColor = "#1E1E2E",
    NodeFillColor   = "#313244",
    NodeStrokeColor = "#89B4FA",
    GroupFillColor  = "#252836",
    GroupStrokeColor = "#45475A",
    EdgeColor       = "#A6ADC8",
    TextColor       = "#CDD6F4",
    TitleTextColor  = "#F8FAFC",
    SubtleTextColor = "#6C7086",
    FontFamily      = "\"JetBrains Mono\", monospace",
    BorderRadius    = 6,
    UseGradients    = true,
    UseBorderGradients = true,
    ShadowStyle     = "soft",
    TransparentBackground = true,
};

string svg = new DiagramRenderer().Render(diagramText, darkTheme);
```

### 9.2 Palette-derived theme example

```csharp
var theme = Theme.FromPalette(
    primaryColor: "#0EA5E9",
    secondaryColor: "#22C55E",
    accentColor: "#F97316",
    backgroundColor: "#F8FAFC");

string svg = new DiagramRenderer().Render(diagramText, theme);
```

### 9.3 Semantic derivation example

```csharp
// Supply just background + foreground + accent; all other slots are derived.
var theme = Theme.FromColors(
    backgroundColor: "#FFFFFF",
    foregroundColor: "#1F2937",
    accentColor:     "#2563EB");

string svg = new DiagramRenderer().Render(diagramText, theme);
```

### 9.4 Presentation theme example

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

### 9.5 Minimal / whiteboard theme

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

## 10. Design notes

- **The `Theme` class is intentionally flat.** No nested objects, no
  inheritance, no style cascading. Every property is a primitive with a
  sensible default. This keeps the API surface minimal and avoids CSS-like
  complexity.
- **Theme properties use CSS color strings** (`#RGB`, `#RGBA`, `#RRGGBB`, or
  `#RRGGBBAA` where applicable). The renderer writes them directly into SVG
  attributes.
- **Font metrics are not theme-aware.** The text-width heuristic
  (`charCount × fontSize × 0.6`) uses a fixed constant regardless of
  `FontFamily`. This is a known limitation — some fonts (e.g., monospace) have
  significantly different advance widths. See
  [layout-architecture.md](layout-architecture.md) Phase 2c for improvement
  plans.
