---
name: publish-rendered-diagrams
description: Rewrites markdown to reference rendered DiagramForge SVGs instead of inline diagram fences, while preserving the original source when possible.
user-invocable: true
---

# Publish Rendered Diagrams

## Purpose

Use this skill when a user wants to convert markdown-authored diagram fences into published SVG image references.

This is an advanced workflow. Prefer preserving the original diagram source rather than deleting it.

## Preferred Command

Recommended safe mode:

```powershell
pwsh scripts/Render-MarkdownDiagrams.ps1 \
  -RootPath <docs-root> \
  -OutputRoot <svg-output-root> \
  -RewriteMarkdown \
  -SourceHandling comment
```

More aggressive mode:

```powershell
pwsh scripts/Render-MarkdownDiagrams.ps1 \
  -RootPath <docs-root> \
  -OutputRoot <svg-output-root> \
  -RewriteMarkdown \
  -SourceHandling remove
```

## Workflow

1. Render diagrams to SVG.
2. Rewrite markdown to use image references.
3. Preserve original source in HTML comments unless the user explicitly wants removal.
4. Report what changed.

## Safety Rules

- Prefer `comment` mode.
- Use `remove` only if the user explicitly wants destructive rewriting or already stores source elsewhere.
- Do not silently delete diagram source.
- Tell the user where rendered assets were written.