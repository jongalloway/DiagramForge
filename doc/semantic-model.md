# Semantic Model Reference

## 1. Overview

The **unified semantic model** is the central data structure in DiagramForge.
Parsers produce it; the layout engine mutates it; the SVG renderer reads it.
No stage communicates with another except through this model.

```
Parser  ─────►  Diagram  ─────►  Layout Engine  ─────►  SVG Renderer
          (populates)        (mutates X/Y/W/H)       (reads everything)
```

All model types live in `DiagramForge.Models`.

---

## 2. Type Map

```
Diagram
 ├── Title              string?
 ├── SourceSyntax       string?
 ├── DiagramType        string?
 ├── LayoutHints        LayoutHints
 ├── Theme?             Theme?
 ├── Nodes              Dictionary<string, Node>
 │    └── Node
 │         ├── Id       string
 │         ├── Label    Label
 │         ├── Shape    Shape (enum)
 │         ├── FillColor / StrokeColor   string?
 │         ├── Metadata Dictionary<string, object>
 │         └── X, Y, Width, Height       double (layout-computed)
 ├── Edges              List<Edge>
 │    └── Edge
 │         ├── SourceId / TargetId       string
 │         ├── Label?   Label?
 │         ├── LineStyle EdgeLineStyle (enum)
 │         ├── ArrowHead ArrowHeadStyle (enum)
 │         ├── IsBidirectional           bool
 │         └── Color?   string?
 └── Groups             List<Group>
      └── Group
           ├── Id       string
           ├── Label    Label
           ├── ChildNodeIds   List<string>
           ├── ChildGroupIds  List<string>
           ├── FillColor / StrokeColor   string?
           └── X, Y, Width, Height       double (layout-computed)
```

---

## 3. Type Reference

### 3.1 `Diagram`

The root container. One `Diagram` instance represents one rendered image.

| Property | Type | Set by | Description |
|----------|------|--------|-------------|
| `Title` | `string?` | Parser | Optional human-readable title displayed above the diagram. |
| `SourceSyntax` | `string?` | Parser | Identifies the parser that produced this model (e.g., `"mermaid"`, `"conceptual"`). |
| `DiagramType` | `string?` | Parser | Specific diagram variant (e.g., `"flowchart"`, `"venn"`, `"mindmap"`). |
| `Nodes` | `Dictionary<string, Node>` | Parser | All nodes, keyed by unique ID. |
| `Edges` | `List<Edge>` | Parser | Directed connections between nodes. |
| `Groups` | `List<Group>` | Parser | Subgraphs / containers that visually group nodes. |
| `LayoutHints` | `LayoutHints` | Parser | Configures direction, spacing, sizing. |
| `Theme` | `Theme?` | Parser (optional) | Diagram-level theme override. When set, takes precedence over the caller-supplied theme. |

**Fluent helpers:** `AddNode`, `AddEdge`, `AddGroup` return `this` for
chaining.

### 3.2 `Node`

A visual element — a box, circle, diamond, etc.

| Property | Type | Set by | Description |
|----------|------|--------|-------------|
| `Id` | `string` | Parser | Unique within the diagram. Used as edge source/target. |
| `Label` | `Label` | Parser | Display text. Defaults to `Id` if not specified. |
| `Shape` | `Shape` | Parser | Visual shape. Default: `RoundedRectangle`. |
| `FillColor` | `string?` | Parser | Overrides `Theme.NodeFillColor` for this node. |
| `StrokeColor` | `string?` | Parser | Overrides `Theme.NodeStrokeColor` for this node. |
| `Metadata` | `Dictionary<string, object>` | Parser | Arbitrary key-value pairs. Not used by layout or rendering; available for custom extensions. |
| `X`, `Y` | `double` | Layout | Top-left position in SVG user units. |
| `Width`, `Height` | `double` | Layout | Dimensions in SVG user units. |

### 3.3 `Edge`

A directed connection between two nodes.

| Property | Type | Set by | Description |
|----------|------|--------|-------------|
| `SourceId` | `string` | Parser | ID of the source node (must exist in `Diagram.Nodes`). |
| `TargetId` | `string` | Parser | ID of the target node (must exist in `Diagram.Nodes`). |
| `Label` | `Label?` | Parser | Optional text displayed at the midpoint of the edge. |
| `LineStyle` | `EdgeLineStyle` | Parser | `Solid`, `Dashed`, `Dotted`, or `Thick`. |
| `ArrowHead` | `ArrowHeadStyle` | Parser | `None`, `Arrow`, `OpenArrow`, `Diamond`, `Circle`. |
| `IsBidirectional` | `bool` | Parser | If true, both ends have arrowheads (not yet rendered). |
| `Color` | `string?` | Parser | Overrides `Theme.EdgeColor` for this edge. |

### 3.4 `Group`

A visual container that groups nodes (Mermaid `subgraph`, or any future
grouping construct).

| Property | Type | Set by | Description |
|----------|------|--------|-------------|
| `Id` | `string` | Parser | Unique within the diagram. |
| `Label` | `Label` | Parser | Header text drawn at the top of the group rectangle. |
| `ChildNodeIds` | `List<string>` | Parser | IDs of nodes belonging to this group. |
| `ChildGroupIds` | `List<string>` | Parser | IDs of nested child groups (future use). |
| `FillColor` | `string?` | Parser | Overrides the default group fill (`#F3F4F6`). |
| `StrokeColor` | `string?` | Parser | Overrides the default group stroke (`#D1D5DB`). |
| `X`, `Y` | `double` | Layout | Top-left position of the bounding rectangle. |
| `Width`, `Height` | `double` | Layout | Dimensions of the bounding rectangle. |

### 3.5 `Label`

Reusable text element attached to nodes, edges, and groups.

| Property | Type | Set by | Description |
|----------|------|--------|-------------|
| `Text` | `string` | Parser | The display text. |
| `Tooltip` | `string?` | Parser | Long-form description (not currently rendered). |
| `FontSize` | `double?` | Parser | Overrides `Theme.FontSize`. |
| `Color` | `string?` | Parser | Overrides `Theme.TextColor`. |

### 3.6 `Shape` (enum)

Determines how the renderer draws a node's background:

| Value | SVG output | Mermaid syntax |
|-------|-----------|---------------|
| `Rectangle` | `<rect rx="0">` | `A[Label]` |
| `RoundedRectangle` | `<rect rx="theme.BorderRadius">` | `A(Label)` |
| `Circle` | `<ellipse>` (equal rx/ry) | `A((Label))` |
| `Ellipse` | `<ellipse>` | — |
| `Pill` / `Stadium` | `<rect rx="height/2">` | — |
| `Diamond` | `<polygon>` (4-point) | `A{Label}` |
| `Hexagon` | (not yet rendered distinctly) | — |
| `Parallelogram` | (not yet rendered distinctly) | — |
| `Cylinder` | (not yet rendered distinctly) | — |

### 3.7 `LayoutHints`

Guides the layout engine's behaviour.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Direction` | `LayoutDirection` | `TopToBottom` | Primary flow direction (`TB`, `BT`, `LR`, `RL`). |
| `HorizontalSpacing` | `double` | `60` | Gap between nodes in the within-layer axis. |
| `VerticalSpacing` | `double` | `40` | Gap between layers. |
| `Alignment` | `NodeAlignment` | `Center` | `Left`, `Center`, `Right` (not yet implemented). |
| `MinNodeWidth` | `double` | `120` | Floor for node width after text sizing. |
| `MinNodeHeight` | `double` | `40` | Uniform node height. |
| `NodePadding` | `double` | `12` | Internal padding inside a node (text inset). |

### 3.8 `EdgeLineStyle` / `ArrowHeadStyle` (enums)

```
EdgeLineStyle:  Solid | Dashed | Dotted | Thick
ArrowHeadStyle: None | Arrow | OpenArrow | Diamond | Circle
```

Currently, only `Solid`, `Dashed`, `Dotted`, `Thick`, `None`, and `Arrow` are
rendered distinctly. The remaining variants render as their closest equivalent.

---

## 4. Ownership & Mutation Rules

| Stage | Reads | Writes |
|-------|-------|--------|
| **Parser** | Input text | All model properties except `X`, `Y`, `Width`, `Height` |
| **Layout engine** | `Nodes`, `Edges`, `Groups`, `LayoutHints`, `Theme` | `Node.X/Y/Width/Height`, `Group.X/Y/Width/Height` |
| **SVG renderer** | Everything | Nothing (read-only) |

**Key invariant:** By the time the layout engine runs, every `Edge.SourceId`
and `Edge.TargetId` must reference a key that exists in `Diagram.Nodes`.
Edges with missing endpoints are silently skipped by the renderer, but the
layout engine's BFS will ignore them (potentially misranking nodes).

**Key invariant:** A `Group.ChildNodeIds` entry must reference a key in
`Diagram.Nodes` for the group's bounding box to be computed. Unknown IDs are
filtered out.

---

## 5. Extending the Model

When adding new properties:

- **Parser-set properties** go on the model type directly. Set them via the
  builder interface (add a new method to `IDiagramSemanticModelBuilder`).
- **Layout-computed properties** go on the model type. The layout engine must
  populate them; the renderer reads them.
- **Rendering-only concerns** (e.g., a CSS class name) should prefer `Node.Metadata`
  or a dedicated rendering options object rather than polluting the semantic model.

When adding a new model type (e.g., `Annotation`, `Swimlane`):

1. Define the class in `Models/`.
2. Add a collection property on `Diagram`.
3. Add a builder method on `IDiagramSemanticModelBuilder` +
   `DiagramSemanticModelBuilder`.
4. Update the layout engine and renderer to handle the new type.
