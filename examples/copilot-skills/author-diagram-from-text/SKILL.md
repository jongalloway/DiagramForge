---
name: author-diagram-from-text
description: Turns prose, bullets, or structured notes into DiagramForge-compatible diagram source. Prefer Mermaid for graph-native diagrams and the Conceptual DSL for slide-native layouts such as matrix, cycle, funnel, chevrons, radial, pillars, and pyramid.
user-invocable: true
---

# Author Diagram From Text

## Purpose

Use this skill when a user wants help creating a diagram from prose, bullets, headings, or a rough concept rather than from an already-written diagram language.

The skill should choose the best DiagramForge syntax for the user's intent and produce valid diagram source that stays within the currently supported DiagramForge feature set.

## Workflow

1. Identify the user's diagram intent.
2. Decide whether the request is better served by Mermaid or the Conceptual DSL.
3. Generate DiagramForge-compatible source text.
4. Prefer explicit, copyable fenced code blocks.
5. Explain any constraints briefly when the requested layout exceeds DiagramForge's current support.

## Selection Rules

Prefer Mermaid for:

- flowchart or process graphs
- sequence interactions
- state transitions
- timelines
- mindmaps
- generic connected node/edge diagrams

Prefer the Conceptual DSL for:

- matrix / quadrant diagrams
- pyramid / layered capability stacks
- cycle / iterative loops
- funnel / narrowing stages
- chevrons / sequential stage process
- radial / hub-and-spoke diagrams
- pillars / parallel capability columns

## Output Rules

- Use only supported DiagramForge syntax.
- When the user wants a polished slide-style diagram, consider the Conceptual DSL first.
- If icon usage helps comprehension, use the shipped icon syntax `icon:pack:name` when the selected diagram type supports it.
- Keep labels short and presentation-friendly.
- Prefer fenced blocks labeled `mermaid` or `diagram` so downstream rendering tools can find them reliably.

## Example Behavior

### Example 1: process pipeline

If the user describes a left-to-right process with decisions or dependencies, output Mermaid flowchart.

### Example 2: executive capability overview

If the user describes a central platform with surrounding capabilities, output a conceptual radial diagram.

### Example 3: prioritization quadrant

If the user wants a 2x2 matrix, output the conceptual matrix DSL rather than forcing a Mermaid approximation.

## Constraints

- Do not invent unsupported Mermaid features.
- Do not claim that Conceptual DSL types exist when they do not.
- If a requested diagram needs a feature that DiagramForge does not support, provide the closest supported source and state the limitation briefly.