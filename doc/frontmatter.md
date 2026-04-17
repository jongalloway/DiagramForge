# Frontmatter

DiagramForge supports a small frontmatter block at the top of a diagram file for embedded theme and styling overrides.

## Syntax

Frontmatter is recognized when the source begins with `---` and contains a closing `---` fence on its own line.

```text
---
theme: dracula
palette: ["#FFB86C", "#8BE9FD", "#50FA7B"]
borderStyle: rainbow
fillStyle: diagonal-strong
shadowStyle: soft
transparent: true
---
flowchart LR
  A[Plan] --> B[Build]
  B --> C[Ship]
```

After the closing fence, the remaining content is parsed normally as Mermaid or Conceptual DSL.

## Supported keys

| Key | Accepted forms | Notes |
| --- | --- | --- |
| `theme` | Built-in theme name | Uses `Theme.GetByName(...)`; unknown names throw. |
| `title` | `title` | Sets `Diagram.Title`; inline parser directives take precedence. |
| `subtitle` | `subtitle` | Sets `Diagram.Subtitle`; rendered below the title in a smaller, muted font. Inline parser directives take precedence. |
| `palette` | Single-line JSON array of hex strings | Example: `["#FF0000", "#00FF00"]` |
| `borderStyle` | `borderStyle`, `border-style` | Values: `solid`, `subtle`, `rainbow` |
| `fillStyle` | `fillStyle`, `fill-style` | Values: `flat`, `subtle`, `diagonal-strong` |
| `shadowStyle` | `shadowStyle`, `shadow-style` | Values: `none`, `soft` |
| `transparent` | `transparent`, `transparentBackground`, `transparent-background` | Boolean: `true` / `false`, `yes` / `no`, `on` / `off`, `1` / `0` |

Quoted values are accepted for string and boolean fields.

## Resolution order

Theme resolution for `DiagramRenderer.Render(...)` is:

```text
frontmatter theme -> parser-assigned Diagram.Theme -> caller theme -> Theme.Default
```

Related overrides are applied in this order:

- Palette: render-method `paletteJson` argument wins over frontmatter `palette`
- Transparency: explicit render-method / CLI override wins over frontmatter `transparent`
- Border, fill, and shadow styles mutate the resolved theme after the base theme is chosen

This lets a diagram carry a default look while still allowing callers or CLI usage to override palette and transparency for a specific render.

## Gotchas

- Frontmatter parsing is intentionally simple. Use one `key: value` per line.
- `palette` must stay on one line and must be valid JSON.
- If a file starts with `---`, DiagramForge will treat it as frontmatter when a closing fence is present. Do not start diagram content with a YAML fence unless that is intentional.
- Unknown theme names or unsupported style values fail fast with `ArgumentException`.
