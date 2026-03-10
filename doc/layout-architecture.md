# Layout Engine — Architecture & Roadmap

## 1. Purpose

This document describes the current DiagramForge layout engine, compares it
with the layout system used by [mermaid-js/mermaid](https://github.com/mermaid-js/mermaid),
and proposes an incremental improvement roadmap.

It is intended to inform contributors and maintainers making design decisions
about layout quality, subgraph handling, edge routing, and when (or whether) to
adopt an external graph-layout library.

---

## 2. Architecture Context

DiagramForge converts diagram text to SVG through a four-stage pipeline:

```
Diagram text  →  Parser  →  Semantic Model  →  Layout Engine  →  SVG Renderer
```

The **layout engine** is the third stage. It receives a fully populated
`Diagram` (nodes, edges, groups, layout hints) and mutates each element's
`X`, `Y`, `Width`, and `Height` in place. The renderer then reads those
coordinates — it never computes positions itself.

The interface is intentionally minimal:

```csharp
public interface ILayoutEngine
{
    void Layout(Diagram diagram, Theme theme);
}
```

This makes the engine swappable: any implementation that assigns coordinates to
every node and group satisfies the contract.

---

## 3. Current Implementation (`DefaultLayoutEngine`)

### 3.1 Sizing Pass

Each node's width is estimated from its label using a character-count
heuristic:

```
width = max(MinNodeWidth, charCount × fontSize × 0.6 + 2 × NodePadding)
```

The constant `0.6` approximates the average glyph advance for Latin sans-serif
fonts (Segoe UI, Inter, Arial). Height is uniform (`MinNodeHeight = 40`).

There is no DOM or font-metrics engine, so the estimate can be ±10% off. The
padding budget absorbs most of the error.

### 3.2 Layer Assignment (Ranking)

Nodes are assigned to layers (ranks) using **Kahn's algorithm** — a BFS-based
topological sort:

1. Compute in-degree for every node from the edge list.
2. Enqueue all nodes with in-degree 0 (roots) at layer 0.
3. For each dequeued node, place every neighbour at
   `max(current_rank, parent_rank + 1)` and decrement its in-degree.
4. When in-degree reaches 0, enqueue the neighbour.

This is equivalent to a **longest-path** heuristic: each node lands at the
deepest layer reachable from any root.

**Cycle handling:** Nodes that never reach in-degree 0 (back-edge participants)
are appended at incrementing ranks after the last BFS layer, in stable sorted
order by node ID.

### 3.3 Coordinate Assignment

Coordinates are assigned in a single forward pass based on `LayoutDirection`:

| Direction | Layer axis | Within-layer axis |
|-----------|-----------|-------------------|
| **TB / BT** | Each layer → horizontal row, advancing in Y | Nodes advance in X by their individual width + `HorizontalSpacing` |
| **LR / RL** | Each layer → vertical column, advancing in X by the widest node in the column | Nodes stack in Y by uniform height + `VerticalSpacing` |

For **RL** and **BT**, a mirror step flips coordinates along the major axis
after placement.

### 3.4 Group Bounding Boxes

Groups (subgraphs) are handled **post-hoc**: after all node positions are
final, each group's bounding rectangle is computed from its member nodes plus
padding. An extra top inset is added when the group has a label, reserving room
for the header text.

If any group extends into negative coordinate space (label padding exceeds
diagram padding), the entire diagram is shifted so nothing clips.

### 3.5 Edge Rendering

Edges are drawn by the SVG renderer (not the layout engine) as **cubic
Bézier curves** between anchor points on node edges:

- The dominant direction (horizontal vs. vertical) between source and target
  determines whether anchors sit on the sides or top/bottom of the nodes.
- Control points are offset 40% of the gap distance, producing a smooth S-curve.
- Edge labels are positioned at the midpoint of the start and end anchors.

There are no bend-points, no edge-routing around intervening nodes, and no
crossing-avoidance logic.

### 3.6 Canvas Sizing

`ComputeWidth` / `ComputeHeight` scan all nodes **and** groups to find the
maximum extent, then add `DiagramPadding`. This ensures group borders that
extend beyond their member nodes are not clipped.

---

## 4. Mermaid.js Layout Architecture

Mermaid supports four pluggable layout engines, selectable per diagram:

| Engine | Algorithm family | Typical use |
|--------|-----------------|-------------|
| **dagre** (default) | Sugiyama layered graph | Flowcharts, state diagrams |
| **ELK** | Eclipse Layout Kernel (Sugiyama + many variants) | Complex flowcharts, nested subgraphs |
| **cose-bilkent** | Force-directed (spring-embedder) | Organic / unstructured layouts |
| **tidy-tree** | Reingold-Tilford | Hierarchical trees, mindmaps |

### 4.1 Dagre (Default Flowchart Engine)

Dagre implements the full **Sugiyama pipeline**, which is the academic
gold-standard for layered graph drawing:

1. **Cycle removal** — back-edges are reversed to make the graph a DAG.
2. **Layer assignment** — network simplex algorithm (optimal-depth ranking,
   minimises total edge length).
3. **Crossing minimization** — barycenter / median heuristics, iterated over
   multiple up-down sweeps to reorder nodes within each layer.
4. **Coordinate assignment** — Brandes-Köpf algorithm for compact, balanced
   positioning that minimises white-space and keeps nodes aligned with their
   predecessors.
5. **Edge routing** — polylines or splines with computed control points to
   route around nodes and minimise crossings.

### 4.2 ELK

ELK provides the same Sugiyama pipeline with additional capabilities:

- Native support for **compound graphs** (nested subgraphs are laid out as
  first-class containers that influence node placement).
- More layout variants (force-based, stress-majorization, radial).
- Configurable per-graph, per-node, and per-edge options.

### 4.3 Text Measurement

Mermaid runs in a browser and uses **DOM `getBBox()`** for pixel-accurate text
measurement. This means node sizes are exact, not estimated.

---

## 5. Comparison

| Aspect | DiagramForge (`DefaultLayoutEngine`) | Mermaid (dagre) |
|--------|--------------------------------------|-----------------|
| **Layer assignment** | BFS longest-path (Kahn's) | Network simplex (optimal) |
| **Crossing minimization** | None (stable ID sort within layers) | Barycenter/median, multi-sweep |
| **Coordinate assignment** | Running offset per layer | Brandes-Köpf (compact, balanced) |
| **Cycle handling** | Append to end layers; no edge reversal | Reverse back-edges; full DAG treatment |
| **Subgraph clustering** | Post-hoc bounding box | Layout-aware: dagre `setParent`, ELK compound nodes |
| **Edge routing** | Cubic Bézier from anchor to anchor; no obstacle avoidance | Polylines / splines with control points routed around nodes |
| **Text measurement** | `charCount × fontSize × 0.6` heuristic | Browser DOM `getBBox()` (pixel-accurate) |
| **Pluggable engines** | 1 (behind `ILayoutEngine`) | 4 (dagre, ELK, cose-bilkent, tidy-tree) |
| **Direction support** | TB, BT, LR, RL | TB, BT, LR, RL |
| **Runtime dependency** | None (pure .NET, no browser) | Browser DOM required |

### 5.1 Where DiagramForge Is Good Enough

For **simple to moderate flowcharts** — linear chains, trees, small DAGs with
a handful of subgraphs — BFS layering + grid placement produces clean,
readable output comparable to dagre's. The variable-width sizing pass and
four-direction support cover the most common real-world cases.

### 5.2 Where the Gap Shows

| Scenario | Symptom |
|----------|---------|
| Many parallel paths with cross-links | Unnecessary edge crossings (no reordering) |
| Dense DAGs (wide fan-out + fan-in) | Suboptimal layer count (longest-path ≠ minimum layers) |
| Nested / overlapping subgraphs | Group rectangles may overlap because members interleave in the same BFS layer |
| Cycles (back-edges) | Cycle nodes are pushed to the end instead of being naturally integrated |
| Long label text | Width estimate can be off by ~10%, occasionally clipping or excess padding |
| Back-edges in flowcharts | No visual distinction (edge appears as a forward long-distance edge) |

---

## 6. Improvement Roadmap

The improvements below are ordered by **impact-to-effort ratio**. Each is
independently shippable and testable.

### Phase 1 — Quick Wins

#### 1a. Crossing Minimization (Barycenter Heuristic)

**Impact:** Large visual improvement for any graph with ≥ 3 nodes per layer.

After layer assignment, reorder nodes within each layer to minimise edge
crossings:

1. For each node in layer *i*, compute the **barycenter** (average position of
   connected nodes in layers *i−1* and *i+1*).
2. Sort the layer by barycenter.
3. Repeat top-down then bottom-up for 4–8 sweeps (convergence is fast).

This is a well-understood algorithm with no API or model changes required — it
operates purely on the layer lists before coordinate assignment.

#### 1b. Cluster-Aware Layer Ordering

**Impact:** Subgraph rectangles stop overlapping in most practical cases.

After barycenter reordering, apply a **grouping constraint**: nodes belonging
to the same `Group` must occupy **adjacent** slots within their layer. This is
a sorting tie-breaker, not a hard constraint, so it does not conflict with
crossing minimisation.

### Phase 2 — Structural Improvements

#### 2a. Edge Reversal for Cycles

Instead of appending cycle members to the end, reverse back-edges to create a
proper DAG (the standard Sugiyama preprocessing step). This allows cycle nodes
to participate in normal layer assignment and appear at their natural depth.
The reversed edges are flagged and rendered with a distinct visual treatment
(e.g., dashed with a curved-back arrowhead).

#### 2b. Edge Routing with Bend-Points

Add a post-layout pass that computes bend-points for edges that would otherwise
cross through intervening nodes:

1. For each edge, check whether the straight path intersects any node bounding
   box.
2. If so, route the edge around the obstacle using a simple offset algorithm.
3. Store bend-points as a `List<(double X, double Y)>` on the `Edge` model.
4. The renderer draws a polyline or multi-segment Bézier through the points.

This requires adding a `BendPoints` property to `Edge` and updating
`AppendEdge` in `SvgRenderer`.

#### 2c. Improved Text Measurement

Replace the fixed `0.6` constant with a **per-character width table** for
common Latin glyphs (derived from Inter or Segoe UI metrics). This narrows the
error band from ±10% to ±2% without requiring a font-loading library.

Alternatively, accept an optional `Func<string, double, double>` text-measure
delegate via `Theme` or `LayoutHints`, allowing callers with access to real
font metrics to plug them in.

### Phase 3 — Advanced Layout Engines

#### 3a. Network Simplex Ranking

Replace the BFS longest-path ranking with the **network simplex** algorithm
for minimum-total-edge-length layer assignment. This produces tighter, more
balanced layouts for complex DAGs.

This is the most algorithm-intensive change; it can be implemented from the
classic Gansner et al. (1993) paper or adapted from the dagre source
(MIT-licensed).

#### 3b. Force-Directed Layout Engine

Add a second `ILayoutEngine` implementation for **non-hierarchical** diagrams
(entity-relationship, organic networks) using a basic spring-embedder (Fruchterman-Reingold).

This would be selected by parsers that produce diagrams without a clear
directional flow.

#### 3c. Tree Layout Engine

Add a **Reingold-Tilford** implementation for strict tree structures (mindmaps,
org charts, hierarchies). The conceptual DSL's `hierarchy` and `mindmap`
diagram types would benefit from this over the BFS grid.

### Phase 4 — External Integration (Optional)

#### 4a. MSAGL Integration

[Microsoft Automatic Graph Layout (MSAGL)](https://github.com/microsoft/automatic-graph-layout)
is a mature .NET graph layout library with Sugiyama, force-directed, and
large-graph layout algorithms. Wrapping MSAGL behind `ILayoutEngine` would
bring production-grade layout quality to DiagramForge without reimplementing
the core algorithms.

**Trade-offs:** MSAGL is a significant dependency (~2 MB, MIT-licensed). It
would be offered as an optional package (e.g., `DiagramForge.Layout.Msagl`),
not a core requirement.

---

## 7. Decision Framework

When choosing which improvements to prioritise, consider:

| Factor | Guidance |
|--------|----------|
| **Diagram complexity ceiling** | If target users rarely exceed 10–15 nodes, Phase 1 alone may be sufficient. |
| **Subgraph usage** | If Mermaid subgraph support is a key feature, Phase 1b + 2b are high priority. |
| **Non-flowchart diagrams** | Conceptual diagrams (cycle, pyramid, matrix) use specialised layout logic in their parsers, so the general engine matters less for those. |
| **Dependency tolerance** | MSAGL (Phase 4) gives the most layout quality per line of code, but adds a large dependency. |
| **Rendering fidelity** | If output is used in slides / print, text measurement accuracy (Phase 2c) matters more than in web previews. |

---

## 8. References

- Gansner, Koutsofios, North, Vo. "A Technique for Drawing Directed Graphs" (1993) — network simplex, Sugiyama pipeline.
- Brandes, Köpf. "Fast and Simple Horizontal Coordinate Assignment" (2001) — coordinate assignment.
- [dagre-js/dagre](https://github.com/dagrejs/dagre) — MIT-licensed JavaScript Sugiyama implementation used by Mermaid.
- [Eclipse ELK](https://www.eclipse.org/elk/) — Eclipse Layout Kernel.
- [microsoft/automatic-graph-layout (MSAGL)](https://github.com/microsoft/automatic-graph-layout) — .NET graph layout library.
- [Mermaid Layouts documentation](https://mermaid.js.org/config/layouts.html) — supported layout engines in Mermaid.
