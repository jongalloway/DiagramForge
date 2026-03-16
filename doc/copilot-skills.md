# Copilot Skill Examples

DiagramForge ships as a library and CLI, but many teams use it through higher-level AI-assisted authoring workflows. This document provides example skill patterns that end users can adapt for their own coding environment.

These assets are examples, not a versioned API contract.

## Goals

The example skills are designed to help with three related tasks:

1. Turn prose or structured notes into DiagramForge-compatible diagram source.
2. Render diagram source to SVG in bulk across a docs or code repository.
3. Optionally replace fenced diagram source in markdown with rendered image references while preserving the original source-of-truth.

## Included Examples

The example assets live under [examples/copilot-skills](../examples/copilot-skills).

### `author-diagram-from-text`

Takes prose, bullets, or tabular intent and chooses the right DiagramForge syntax:

- Mermaid when the diagram is a natural node/edge graph.
- Conceptual DSL when the desired output is presentation-native, such as matrix, pyramid, cycle, funnel, chevrons, radial, or pillars.

### `render-diagrams-to-svg`

Finds supported diagram fences in markdown and renders them to SVG using DiagramForge.

The companion script is [scripts/Render-MarkdownDiagrams.ps1](../scripts/Render-MarkdownDiagrams.ps1).

### `publish-rendered-diagrams`

Advanced optional workflow that rewrites markdown to use rendered SVGs instead of inline diagram fences.

This example is intentionally conservative: it recommends preserving the original source in a collapsible `<details>` block rather than deleting it outright.

## Recommended Workflow

### 1. Author source

Use the `author-diagram-from-text` example to generate diagram source in markdown fences such as:

````text
```mermaid
flowchart LR
  A[Plan] --> B[Build]
  B --> C[Ship]
```
````

or:

````text
```diagram
diagram: radial
center: Platform
items:
  - Security
  - Reliability
  - Developer Experience
  - Observability
```
````

Use explicit fence labels for predictable bulk rendering:

- `mermaid`
- `diagram`
- `diagramforge`
- `conceptual`

### 2. Render to SVG

Run the render script against a docs tree or repository subtree:

```powershell
pwsh scripts/Render-MarkdownDiagrams.ps1 -RootPath docs -OutputRoot docs/generated/diagrams
```

This scans markdown files for supported diagram fences, renders each block through DiagramForge, and writes stable SVG outputs into the chosen output root.

### 3. Publish rendered output

If you want markdown to reference the rendered images instead of containing the original fences inline, use rewrite mode:

```powershell
pwsh scripts/Render-MarkdownDiagrams.ps1 \
  -RootPath docs \
  -OutputRoot docs/generated/diagrams \
  -RewriteMarkdown \
  -SourceHandling details
```

Recommended source handling:

- `details`: replaces the fence with an image reference and preserves the original diagram source in a collapsible `<details>` block. Safe for any diagram syntax, including Mermaid (which uses `--` and `-->`).
- `remove`: replaces the fence with an image reference and removes the inline source. Use only if your source is preserved somewhere else.

## Why Preserve Source?

Diagram source should remain the editable source-of-truth whenever possible.

Preserving source avoids three common problems:

1. Review diffs become less informative when only generated SVG changes are visible.
2. Regeneration is harder if the source diagram text is lost.
3. Editing workflows degrade if authors need to recover source from rendered output or repository history.

## Diagram Selection Guidance

Use Mermaid for:

- Flowcharts
- Sequences
- State diagrams
- Mindmaps
- Timelines
- Generic relationship diagrams

Use the Conceptual DSL for:

- Matrix
- Pyramid
- Cycle
- Funnel
- Chevrons
- Radial
- Pillars

The example authoring skill explicitly encourages use of the full DiagramForge capabilities, including conceptual diagrams, when the desired output is more slide-native than graph-native.

## Adapting These Examples

Different coding environments expose skills differently. Some support workspace-level skills, some support user-level skills, and others rely on prompts or instructions files.

These examples are meant to be copied and adapted rather than used as a rigid packaging format. The important part is the workflow logic:

1. choose the right diagram language,
2. generate valid DiagramForge source,
3. render SVGs consistently,
4. preserve source safely during publishing.
