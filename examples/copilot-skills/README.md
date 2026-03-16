# Example Copilot Skills

This folder contains example skill assets that end users can adapt for DiagramForge workflows in their own coding environment.

These are examples, not a guaranteed public contract.

## Included Skills

- [author-diagram-from-text](author-diagram-from-text/SKILL.md)
- [render-diagrams-to-svg](render-diagrams-to-svg/SKILL.md)
- [publish-rendered-diagrams](publish-rendered-diagrams/SKILL.md)

## Intended Use

The examples cover a common documentation workflow:

1. Generate Mermaid or Conceptual DSL source from prose or bullets.
2. Render all diagram fences in markdown to SVG.
3. Optionally replace fences with rendered image references while preserving the original source.

The companion bulk-render script is [scripts/Render-MarkdownDiagrams.ps1](../../scripts/Render-MarkdownDiagrams.ps1).

More background and usage guidance is in [doc/copilot-skills.md](../../doc/copilot-skills.md).