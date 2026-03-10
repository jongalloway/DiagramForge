# Parser Architecture

## 1. Overview

DiagramForge uses a **pluggable parser architecture** to convert raw diagram
text into a unified semantic model. Each supported syntax (Mermaid, Conceptual
DSL, future formats) has its own parser implementation behind a common
interface. The renderer and layout engine never see syntax-specific details —
they consume only the `Diagram` model that parsers produce.

```
               ┌─ MermaidParser ──────── MermaidFlowchartParser
Input text ──► │                         MermaidMindmapParser
               │                         MermaidStateParser
               │
               ├─ ConceptualDslParser
               │
               └─ (future: D2Parser, DotParser, …)
                            │
                            ▼
                     Diagram model
```

---

## 2. Core Interfaces

### `IDiagramParser`

Every parser implements this interface:

```csharp
public interface IDiagramParser
{
    string SyntaxId { get; }
    bool CanParse(string diagramText);
    Diagram Parse(string diagramText);
}
```

| Member | Purpose |
|--------|---------|
| `SyntaxId` | A short lowercase identifier (e.g., `"mermaid"`, `"conceptual"`). Used in error messages and the `RegisteredSyntaxes` list. |
| `CanParse` | Fast heuristic — inspects the first content line to decide whether this parser can handle the input. Must not throw. |
| `Parse` | Full parse. Returns a populated `Diagram` or throws `DiagramParseException`. |

### `IDiagramSemanticModelBuilder`

A fluent builder that parsers use to construct a `Diagram` incrementally:

```csharp
public interface IDiagramSemanticModelBuilder
{
    IDiagramSemanticModelBuilder WithTitle(string title);
    IDiagramSemanticModelBuilder WithSourceSyntax(string syntaxId);
    IDiagramSemanticModelBuilder WithDiagramType(string diagramType);
    IDiagramSemanticModelBuilder AddNode(Node node);
    IDiagramSemanticModelBuilder AddEdge(Edge edge);
    IDiagramSemanticModelBuilder AddGroup(Group group);
    IDiagramSemanticModelBuilder WithLayoutHints(LayoutHints hints);
    IDiagramSemanticModelBuilder WithTheme(Theme theme);
    Diagram Build();
}
```

The default implementation (`DiagramSemanticModelBuilder`) is a thin wrapper
that populates the `Diagram` properties directly. Parsers should always use the
builder rather than constructing `Diagram` objects by hand — this keeps the
construction contract in one place and makes it easier to add validation or
normalization later.

### `DiagramParseException`

Thrown when input cannot be parsed. Includes an optional `LineNumber` property
so callers can report the exact location of the error.

---

## 3. Parser Discovery & Selection

`DiagramRenderer` maintains an ordered list of `IDiagramParser` instances:

```csharp
private readonly List<IDiagramParser> _parsers;
```

When `Render(diagramText)` is called, the renderer iterates the list and calls
`CanParse` on each parser. The **first parser that returns true** handles the
input. If no parser matches, a `DiagramParseException` is thrown listing all
registered syntax IDs.

User-registered parsers (via `RegisterParser`) are **prepended** to the list,
so they take priority over the built-in parsers. This allows callers to
override the default Mermaid parser or add entirely new syntaxes.

Default registration order:

1. `MermaidParser`
2. `ConceptualDslParser`

---

## 4. The Mermaid Parser (Two-Level Dispatch)

The Mermaid parser uses a **facade + sub-parser** pattern because Mermaid is
not one language — it's a family of diagram types (`flowchart`, `mindmap`,
`stateDiagram`, etc.) that share only the comment syntax (`%%`) and keyword
header convention.

### 4.1 `MermaidDocument` — Pre-processing

Before any diagram-specific parsing, `MermaidDocument.Parse()`:

1. Splits the input into lines.
2. Strips blank lines and `%%`-prefixed comments.
3. Produces two line arrays:
   - `Lines` — fully trimmed (used by most parsers).
   - `RawLines` — trailing whitespace trimmed but leading whitespace preserved
     (used by the mindmap parser, which relies on indentation).
4. Detects the `MermaidDiagramKind` from the first content line's keyword
   (`flowchart`, `graph`, `mindmap`, `stateDiagram`, etc.).

Known but unsupported keywords (e.g., `sequenceDiagram`, `gantt`) are
classified as `Unknown` so that `MermaidParser.CanParse` returns true and the
subsequent `Parse` call can throw a specific "unsupported type" error rather
than falling through to the Conceptual parser.

### 4.2 `MermaidParser` — Facade

The facade holds a list of `IMermaidDiagramParser` sub-parsers:

```csharp
internal interface IMermaidDiagramParser
{
    bool CanParse(MermaidDiagramKind kind);
    Diagram Parse(MermaidDocument document);
}
```

It dispatches to the first sub-parser whose `CanParse(document.Kind)` returns
true. Currently registered sub-parsers:

| Sub-parser | `MermaidDiagramKind` | Diagram type |
|-----------|---------------------|-------------|
| `MermaidFlowchartParser` | `Flowchart` | `flowchart` |
| `MermaidMindmapParser` | `Mindmap` | `mindmap` |
| `MermaidStateParser` | `StateDiagram` | `statediagram` |

### 4.3 `MermaidFlowchartParser`

The most complex sub-parser. It handles:

- **Direction detection:** `LR`, `RL`, `TB/TD`, `BT` from the header line.
- **Node declarations:** `A[Label]`, `B(Rounded)`, `C{Diamond}`, `D((Circle))`,
  `E>Flag]`. Parsed by the shared `MermaidNodeSyntax.ParseNodeDeclaration`.
- **Edges:** `-->`, `--->`, `-.->`, `==>`, `---`, `-.-`, `===`, `-->>`. Parsed
  by the shared `MermaidEdgeSyntax.FindOperator`, with line/arrow style derived
  from the operator characters.
- **Edge labels:** Both `A -->|label| B` and `A |label|--> B` syntax.
- **Subgraphs:** `subgraph [id] [title]` / `end` blocks, tracked via a stack.
  Nodes declared inside a subgraph are added to the group's `ChildNodeIds`.
- **De-duplication:** Nodes are created on first reference and reused on
  subsequent references (via `GetOrCreateNode`), so `A --> B` followed by
  `B --> C` shares the same `B` node.

### 4.4 `MermaidMindmapParser`

Parses indent-based trees. Uses `RawLines` (leading whitespace preserved) to
determine parent-child relationships via a stack: each line's indentation is
compared to the stack to find the nearest less-indented ancestor.

### 4.5 `MermaidStateParser`

Similar to the flowchart parser but with state-diagram-specific conventions:

- `[*]` tokens are mapped to synthetic `__start__` / `__end__` circle nodes.
- Transition labels use `-->` with a `: label` suffix on the target side.
- State definitions use `id : Label` syntax.

### 4.6 Shared Syntax Helpers

Two `internal static` classes extract syntax-level concerns that are shared
across sub-parsers:

| Class | Responsibility |
|-------|---------------|
| `MermaidNodeSyntax` | Parses `ID[Label]`-style tokens → `(id, label, shape)` |
| `MermaidEdgeSyntax` | Finds edge operators, determines `EdgeLineStyle` and `ArrowHeadStyle` |

---

## 5. The Conceptual DSL Parser

A single-class parser (`ConceptualDslParser`) for the YAML-inspired Conceptual
DSL:

```
diagram: process
steps:
  - Discover
  - Plan
  - Build
```

### 5.1 Detection

`CanParse` checks whether the first content line matches
`diagram: <known-type>`. Known types: `process`, `cycle`, `hierarchy`, `venn`,
`list`, `matrix`, `pyramid`.

### 5.2 Parsing Strategy

The DSL is simple enough that it does not need a tokenizer — the parser works
directly on trimmed lines:

| Diagram type | Section key | Structure |
|-------------|------------|-----------|
| `process` | `steps:` | Linear chain with edges |
| `cycle` | `items:` | Chain + back-edge from last to first |
| `list` | `items:` | Nodes only, no edges |
| `venn` | `sets:` | Nodes only |
| `pyramid` | `levels:` | Nodes only |
| `matrix` | `rows:` + `columns:` | Grid of `cell_{r}_{c}` nodes |
| `hierarchy` | (indentation-based) | Tree via indent stack (like mindmap) |

The parser sets `LayoutDirection` per type: `LeftToRight` for most types,
`TopToBottom` for hierarchy.

---

## 6. How to Add a New Parser

### 6.1 Adding a New Mermaid Diagram Type

1. Add a variant to `MermaidDiagramKind`.
2. Add the keyword to `MermaidDocument.TryDetectKind`.
3. Create a new `IMermaidDiagramParser` implementation (e.g.,
   `MermaidSequenceParser`).
4. Register it in `MermaidParser._diagramParsers`.
5. Remove the keyword from `KnownUnsupportedMermaidKeywords` if it was listed.
6. Add unit tests in `MermaidParserTests` and an E2E fixture.

### 6.2 Adding an Entirely New Syntax

1. Create a class implementing `IDiagramParser`.
2. Implement `SyntaxId`, `CanParse`, and `Parse`.
3. Use `DiagramSemanticModelBuilder` to construct the `Diagram`.
4. The parser is available to callers via `RegisterParser`:

   ```csharp
   var renderer = new DiagramRenderer().RegisterParser(new MyD2Parser());
   ```

   Or, to include it in the default set, add it to the `DiagramRenderer`
   default constructor's parser list.

5. Add unit tests and E2E fixtures.

### 6.3 Guidelines

- **`CanParse` must be cheap.** It's called for every parser on every input.
  Inspect only the first non-comment line.
- **Parsers must not depend on layout or rendering.** They produce a model;
  they never compute coordinates or emit SVG.
- **Set `SourceSyntax` and `DiagramType`.** Downstream code (logging, error
  messages, future routing) relies on these metadata fields.
- **Set `LayoutHints.Direction`.** The layout engine respects whatever the
  parser sets; omitting it defaults to `TopToBottom`.
- **Use the builder.** Don't construct `Diagram` directly. The builder is the
  documented construction path and may gain validation in the future.
- **Throw `DiagramParseException`** with a clear message and `LineNumber` when
  possible.

---

## 7. Parser Test Strategy

| Layer | What to test | Test class |
|-------|-------------|-----------|
| Unit | `CanParse` true/false, node/edge/group counts, labels, shapes, directions, error cases | `MermaidParserTests`, `ConceptualDslParserTests` |
| E2E | Full `text → SVG` rendering matches snapshot baseline | `SnapshotTests` (via `Fixtures/*.input`) |

When adding a new parser or diagram type, add both unit tests (for parse logic)
and an E2E fixture (for visual regression).
