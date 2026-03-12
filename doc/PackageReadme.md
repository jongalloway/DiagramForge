# DiagramForge

Text in, SVG out. DiagramForge is a .NET library and CLI for rendering diagram text to real SVG without a browser, JavaScript runtime, or headless Chrome.

DiagramForge focuses on the cases where Mermaid-style diagram authoring is useful, but browser-generated SVG is not: PowerPoint decks, Keynote slides, Inkscape workflows, docs pipelines, and image conversion with tools like librsvg.

![Diagram gallery](https://raw.githubusercontent.com/jongalloway/DiagramForge/main/doc/diagram-gallery.svg)

## Why DiagramForge

- Real SVG output with native `text` elements
- No browser, Node.js, or headless Chrome dependency
- Deterministic rendering suitable for snapshot testing
- Mermaid subset support plus presentation-oriented conceptual layouts
- Built-in themes, JSON themes, palette overrides, and embedded frontmatter styling

![Theme gallery](https://raw.githubusercontent.com/jongalloway/DiagramForge/main/doc/theme-gallery.svg)

## Install

DiagramForge targets `.NET 10`.

### Library

```bash
dotnet add package DiagramForge
```

### CLI tool

```bash
dotnet tool install -g DiagramForge.Tool
```

## Basic usage

```csharp
using DiagramForge;

var renderer = new DiagramRenderer();
string svg = renderer.Render("""
flowchart LR
  A[Plan] --> B[Build]
  B --> C[Ship]
""");
```

## CLI usage

```bash
diagramforge diagram.mmd -o diagram.svg
diagramforge diagram.mmd --theme dracula --transparent -o overlay.svg
```

## Supported today

- Mermaid subset: flowchart, block, sequence, state, mindmap, timeline, venn, architecture, and xychart
- Conceptual DSL: matrix, pyramid, cycle, and pillars
- Theme presets, theme JSON files, palette overrides, and frontmatter styling

DiagramForge intentionally implements a focused Mermaid subset rather than full Mermaid.js parity.

## Documentation

- Full project README: [github.com/jongalloway/DiagramForge](https://github.com/jongalloway/DiagramForge)
- Theming guide: [Theming](https://github.com/jongalloway/DiagramForge/blob/main/doc/theming.md)
- Frontmatter guide: [Frontmatter](https://github.com/jongalloway/DiagramForge/blob/main/doc/frontmatter.md)
- Product scope and roadmap: [PRD](https://github.com/jongalloway/DiagramForge/blob/main/doc/prd.md)

## Feedback

- Issues: [github.com/jongalloway/DiagramForge/issues](https://github.com/jongalloway/DiagramForge/issues)
- Source: [github.com/jongalloway/DiagramForge](https://github.com/jongalloway/DiagramForge)
