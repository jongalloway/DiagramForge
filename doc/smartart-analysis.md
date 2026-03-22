# SmartArt Analysis for Future Conceptual Diagrams

This document reviews the Microsoft SmartArt catalog and recommends which presentation-native diagram types DiagramForge should add next.

Reference source:

- Microsoft Support: [All SmartArt graphics, described](https://support.microsoft.com/en-us/office/all-smartart-graphics-described-cf1a453b-de4a-4217-8da0-1aff97bb32cd)

## Purpose

DiagramForge should not chase SmartArt parity. The product direction in [prd.md](prd.md) is to support modern equivalents for slide-friendly conceptual diagrams, not to reproduce Office layouts exactly.

That means the best additions are the ones that:

- solve common presentation problems,
- are awkward to express cleanly in Mermaid,
- fit the existing parser -> semantic model -> layout -> SVG pipeline,
- and can reuse current conceptual architecture rather than introducing a new rendering system.

## Current Coverage

DiagramForge already covers a meaningful part of the high-value SmartArt space through its conceptual DSL and Mermaid support.

Conceptual diagram types currently implemented in the repository:

- matrix
- pyramid
- cycle
- funnel
- pillars
- radial
- target
- chevrons
- tree / org-chart style tree
- snake timeline
- tablist

That is important because several SmartArt categories are already effectively represented:

- Process: chevrons, funnel, snake timeline, Mermaid timeline
- Cycle: cycle, radial
- Hierarchy: tree / org chart style tree
- Matrix: 2x2 matrix
- Pyramid: pyramid
- List: tablist and several existing text-first conceptual layouts

## Selection Criteria

The SmartArt catalog is broad, but much of it falls into one of three buckets:

1. Useful and presentation-native
2. Already covered well enough by Mermaid or existing DiagramForge concepts
3. Office-specific or picture-heavy layouts that do not align with DiagramForge's text-to-SVG model

The recommendations below favor category 1.

## Recommended Additions

### 1. Target / Concentric Target Diagrams

Status: implemented after this analysis.

Highest-value next addition.

Relevant SmartArt examples:

- Basic Target
- Nested Target
- Target List

Why it is valuable:

- Common in strategy, planning, segmentation, maturity, priority, and audience slides
- Strong visual payoff in presentations
- Not idiomatic in Mermaid
- Distinct from current matrix, radial, and pyramid layouts

Why it fits DiagramForge:

- Can be implemented as a specialized conceptual layout with a small SVG extension
- Reuses the existing node model and metadata-driven rendering approach
- Works well with current theming, labels, and icon support patterns

Suggested scope:

- Concentric rings with 3-6 levels
- Optional center label
- Optional side labels or callouts for each ring

### 2. Matrix Variants

High-value, low-risk extension of what already exists.

Relevant SmartArt examples:

- Basic Matrix
- Grid Matrix
- Titled Matrix

Why it is valuable:

- Matrix slides are common in prioritization, capability mapping, risk, and portfolio discussions
- The current 2x2 matrix is useful but constrained
- Titled and grid variants cover a wider range of real presentation use cases

Why it fits DiagramForge:

- Existing matrix parser and layout provide a direct starting point
- This is an incremental extension rather than a new conceptual family
- The renderer already handles metadata-driven special cases cleanly

Suggested scope:

- Titled matrix with a center title and four surrounding quadrants
- 3x3 grid matrix for placement along two axes
- Preserve the current 2x2 form as the default

### 3. Richer Hierarchy / Org-Chart Variants

Hierarchy remains one of the most useful SmartArt families in business presentations.

Relevant SmartArt examples:

- Hierarchy
- Organization Chart
- Horizontal Hierarchy
- Labeled Hierarchy
- Table Hierarchy

Why it is valuable:

- Org charts, responsibility trees, capability maps, and decomposition views are common slide artifacts
- Mermaid mindmap and flowchart are often serviceable, but formal presentation hierarchies benefit from dedicated layout rules

Why it fits DiagramForge:

- The repository already has tree parsing and an org-chart style preset
- The next value is in variants, not another generic hierarchy type
- Horizontal, labeled, or assistant-aware hierarchy behaviors can extend the current tree implementation

Suggested scope:

- Horizontal hierarchy variant
- Labeled hierarchy variant
- Org-chart-specific assistant or hanging branch options

### 4. Compare / Opposing-Ideas Layouts

Useful for decision, tradeoff, and pros-versus-cons slides.

Relevant SmartArt examples:

- Balance
- Opposing Ideas
- Plus and Minus
- Counterbalance Arrows
- Opposing Arrows

Why it is valuable:

- These are common executive and product-review slides
- They express comparison more clearly than a generic flowchart
- The visual form is more important than graph semantics

Why it fits DiagramForge:

- It is presentation-native and text-first
- It can likely be implemented with standard nodes plus specialized placement
- It complements current conceptual types without overlapping Mermaid too much

Suggested scope:

- Two-sided comparison layout
- Optional central thesis / pivot / decision node
- Support for grouped bullets or sub-points on each side

### 5. Radial Variants

Worth doing after target and matrix expansion.

Relevant SmartArt examples:

- Radial List
- Radial Cluster
- Converging Radial
- Diverging Radial
- Basic Radial

Why it is valuable:

- Hub-and-spoke is already useful, but more expressive radial forms would broaden the design language for strategy and architecture slides
- This supports narratives like inputs to a center, outputs from a center, or clustered concepts around a theme

Why it fits DiagramForge:

- Existing radial support provides the parser and layout foundation
- Variants can be introduced as alternate layout modes rather than as fully separate infrastructure

Suggested scope:

- `layout: cluster`
- `layout: converging`
- `layout: diverging`

### 6. Pyramid Variants

Useful, but lower priority than the items above.

Relevant SmartArt examples:

- Inverted Pyramid
- Segmented Pyramid
- Pyramid List

Why it is valuable:

- Supports maturity, hierarchy, containment, and emphasis narratives
- Common in strategy and transformation presentations

Why it fits DiagramForge:

- Current pyramid and funnel implementations already cover much of the geometry needed
- Variants are likely incremental and renderer-friendly

Suggested scope:

- inverted pyramid
- segmented pyramid
- optional side labels or captions

## Lower-Priority SmartArt Families

These are less attractive for DiagramForge right now.

### Picture-heavy layouts

Examples:

- Picture Accent List
- Picture Caption List
- Picture Grid
- Picture Organization Chart

Why lower priority:

- DiagramForge is strongest when the source is text-first and theme-driven
- Picture-centric SmartArt depends on image assets and a different authoring model
- These layouts are less reusable in documentation and automation workflows

### SmartArt variants already covered by Mermaid or current diagrams

Examples:

- Venn and overlap diagrams
- Generic process chains
- Timelines and phased roadmaps
- Generic relationship graphs

Why lower priority:

- Mermaid already covers these categories well enough for DiagramForge's current scope
- Adding conceptual duplicates would increase surface area without clear user value

### Decorative Office-specific process shapes

Examples:

- Gear
- Equation
- Pie Process
- Ascending / Descending process variants that mainly restyle the same story

Why lower priority:

- They are more ornamental than foundational
- Many are style variants of shapes already represented by chevrons, funnel, cycle, or timeline concepts

## Recommended Priority Order

If the goal is to add the next few conceptual diagram types with strong product value and good implementation fit, the order should be:

1. Target / concentric target
2. Matrix variants: titled matrix and 3x3 grid matrix
3. Hierarchy / org-chart variants
4. Compare / opposing-ideas layout
5. Radial variants
6. Pyramid variants

## Practical Guidance

The repository already shows the right extension seam for conceptual diagrams:

- add a parser entry for the new diagram type,
- add a conceptual layout handler,
- add only the SVG specialization needed for the final shape,
- cover the work with focused parser tests and snapshot fixtures.

That favors additions like target diagrams and matrix variants, which are mostly specialized layout and rendering work, over more ambitious diagram families that would require new semantic primitives.

## Summary

The best next SmartArt-inspired additions are not the broadest or flashiest categories. They are the presentation-native layouts that:

- appear frequently in modern business and product presentations,
- are clearly better than Mermaid for the same job,
- and can be implemented incrementally within DiagramForge's existing conceptual DSL architecture.

From that perspective, target diagrams, matrix variants, and hierarchy variants are the strongest next investments.
