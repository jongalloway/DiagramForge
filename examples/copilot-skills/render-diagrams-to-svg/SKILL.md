---
name: render-diagrams-to-svg
description: Finds DiagramForge-compatible diagram fences in markdown and renders them to SVG in bulk using the repository's render workflow.
user-invocable: true
---

# Render Diagrams To SVG

## Purpose

Use this skill when a user wants to render one or more markdown-embedded diagrams to SVG files using DiagramForge.

This skill is intended for repository or documentation workflows where diagram source remains in markdown and rendered SVGs are generated as build or publish artifacts.

## Preferred Command

Use the companion script:

```powershell
pwsh scripts/Render-MarkdownDiagrams.ps1 -RootPath <docs-root> -OutputRoot <svg-output-root>
```

## Expected Fence Labels

The script is designed to find diagram fences labeled with:

- `mermaid`
- `diagram`
- `diagramforge`
- `conceptual`

## Workflow

1. Identify the markdown root to scan.
2. Run the render script with an explicit output directory.
3. Report how many markdown files and diagram fences were rendered.
4. If rendering fails, surface the failing file and fence index.

## Guidance

- Prefer rendering without rewriting markdown first.
- Keep source markdown as the source-of-truth unless the user explicitly wants a publishing rewrite.
- Use this skill as the default "build rendered diagrams" step after authoring.