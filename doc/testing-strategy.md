# Testing Strategy

## 1. Overview

DiagramForge uses a **two-tier testing approach**: fast unit tests for isolated
component logic, and end-to-end snapshot tests for full-pipeline visual
regression. Both tiers run in CI on every push and PR.

---

## 2. Test Infrastructure

| Aspect | Detail |
|--------|--------|
| **Framework** | xUnit v3 |
| **Runner** | Microsoft Testing Platform (MTP) — test projects are self-contained executables |
| **Assertion** | xUnit built-in `Assert.*` |
| **Snapshot comparison** | XML-canonical via `XDocument.Parse` |
| **CI guard** | `UPDATE_SNAPSHOTS` env var blocked in CI |

---

## 3. Test Projects

### 3.1 `DiagramForge.Tests` — Unit Tests

Location: `tests/DiagramForge.Tests/`

Tests individual components in isolation:

| Test class | Component under test | What it verifies |
|-----------|---------------------|-----------------|
| `DiagramRendererTests` | `DiagramRenderer` (full pipeline) | End-to-end SVG output contains expected elements; parser selection works |
| `MermaidParserTests` | `MermaidParser` + sub-parsers | `CanParse` true/false, node counts, edge counts, labels, shapes, directions, subgraph membership, edge styles, error messages |
| `ConceptualDslParserTests` | `ConceptualDslParser` | `CanParse` true/false, process/cycle/hierarchy/venn/list/matrix/pyramid node/edge counts and labels |
| `DefaultLayoutEngineTests` | `DefaultLayoutEngine` | Position ordering (A.Y < B.Y in TB), non-negative coordinates, width > 0, direction-dependent axis, group bounding boxes |
| `SvgRendererTests` | `SvgRenderer` | SVG root element, node labels present, title, arrowhead markers, edge labels, theme colors in output |
| `DiagramModelTests` | `Diagram`, `Node`, `Edge`, `Group` | Fluent API chaining, add/retrieve by ID, collection population |

**Testing philosophy:**

- Tests construct models directly using the public API (`new Diagram()`,
  `new Node()`, etc.) — no test fixtures or builders.
- Layout tests use the real `DefaultLayoutEngine` and `Theme.Default` —
  they verify coordinate relationships, not exact pixel values.
- Renderer tests call the real `SvgRenderer` after layout and assert on
  string contents (node labels, SVG structure), not exact byte output.

### 3.2 `DiagramForge.E2ETests` — Snapshot Tests

Location: `tests/DiagramForge.E2ETests/`

Full-pipeline regression tests: raw diagram text → `DiagramRenderer.Render()`
→ SVG string compared to a golden baseline.

---

## 4. Snapshot Test Mechanics

### 4.1 Fixture Structure

Each test case is a pair of files in `tests/DiagramForge.E2ETests/Fixtures/`:

```
mermaid-flowchart-lr.input           ← raw diagram text
mermaid-flowchart-lr.expected.svg    ← golden baseline SVG
```

Fixtures are discovered dynamically via directory glob — adding a new
`.input` file is all that's needed to create a new test case.

### 4.2 Test Flow

```
1. Glob all *.input files
2. For each fixture:
   a. Read .input text
   b. Render through DiagramRenderer
   c. Write .actual.svg to artifacts/ (always, even on pass)
   d. If UPDATE_SNAPSHOTS=1: write .expected.svg and return
   e. Otherwise: compare actual vs expected using XML-canonical comparison
```

### 4.3 XML-Canonical Comparison

Instead of byte-for-byte string comparison, both the expected and actual SVG
are parsed through `XDocument.Parse` and re-serialized with
`SaveOptions.None`. This normalises:

- Insignificant whitespace
- Attribute serialisation order

So only **structural or value changes** cause failures — not formatting
differences.

### 4.4 Artifact Output

Every run writes rendered SVGs to:

```
{repo}/artifacts/test-results/snapshots/{fixture}.actual.svg
```

This directory is gitignored but uploaded as a CI build artifact, so reviewers
can download and inspect exactly what a PR renders — even for passing tests.

### 4.5 Baseline Regeneration

When a code change intentionally alters rendering output:

```powershell
# PowerShell
$env:UPDATE_SNAPSHOTS = '1'; dotnet test tests/DiagramForge.E2ETests; Remove-Item Env:UPDATE_SNAPSHOTS
```

```bash
# bash
UPDATE_SNAPSHOTS=1 dotnet test tests/DiagramForge.E2ETests
```

This writes new `.expected.svg` files into the **source** fixtures directory.
Review the diff in source control and commit alongside the code change.

### 4.6 CI Safety

`UPDATE_SNAPSHOTS` is forced to `0` in CI (detected via `CI`, `TF_BUILD`, or
`GITHUB_ACTIONS` env vars). The test explicitly asserts:

```csharp
Assert.False(UpdateSnapshots && IsCi,
    "UPDATE_SNAPSHOTS is set in a CI environment. ...");
```

This prevents accidental auto-acceptance of broken baselines on the build
agent.

### 4.7 Empty-Directory Guard

A standalone `[Fact]` test verifies that the fixtures directory exists and
contains at least one `.input` file. Without this, a broken copy-to-output
glob would silently produce a green build with zero test execution.

---

## 5. Coverage Goals

There are no hard coverage thresholds enforced in CI. The guiding principles
are:

| Component | Coverage expectation | Rationale |
|-----------|---------------------|-----------|
| **Parsers** | High (>90%) | User-facing input handling; must cover valid syntax, edge cases, and error paths |
| **Layout engine** | Medium-High | Coordinate relationships matter; exact values are fragile to test |
| **SVG renderer** | Medium | Structural presence of elements (labels, shapes, edges) more valuable than exact attribute values |
| **Model types** | Low-Medium | Mostly data holders; unit tests confirm fluent API and storage correctness |

Run coverage locally:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## 6. How to Write Good Tests

### 6.1 Parser Tests

- **Test `CanParse` with positive and negative cases.** Include inputs that
  _almost_ match (e.g., a misspelled keyword).
- **Test structural outcomes:** node count, edge count, group membership,
  direction.
- **Test labels and shapes** — these are the primary parser output.
- **Test error paths:** invalid input should throw `DiagramParseException`
  with a clear message.
- **Don't test coordinates** — that's the layout engine's job.

### 6.2 Layout Tests

- **Test coordinate relationships, not absolute values.** Assert `A.Y < B.Y`,
  not `A.Y == 24.0`. This makes tests resilient to theme/spacing changes.
- **Test direction-dependent behaviour:** TB vs LR should advance along
  different axes.
- **Test edge cases:** single node, disconnected nodes, cycles,
  empty diagrams, groups with no members.
- **Test group bounding boxes:** group coordinates should enclose all member
  nodes.

### 6.3 Renderer Tests

- **Assert on content presence** (`Assert.Contains("label text", svg)`), not
  exact SVG strings.
- **Use the real layout engine** — the renderer depends on coordinates being
  populated.
- **Test theme application:** verify that custom fill/stroke colors appear in
  the output.

### 6.4 Snapshot Tests

- **One fixture per visual scenario.** Name fixtures descriptively:
  `mermaid-flowchart-lr`, `conceptual-cycle`, `mermaid-subgraph`.
- **Review baselines carefully.** When regenerating, inspect the diff in a
  browser — open both the `.expected.svg` and `.actual.svg` side by side.
- **Keep fixtures minimal.** A fixture should exercise one feature, not be a
  complex real-world diagram. Complex diagrams are better tested as unit tests
  on specific parser/layout behaviours.

---

## 7. Adding a New Test for a New Feature

1. **Unit tests first.** Add tests to the appropriate `*Tests.cs` class for
   the parser, layout, or renderer change.
2. **Add an E2E fixture.** Drop a `.input` file in `Fixtures/`, run with
   `UPDATE_SNAPSHOTS=1`, review the generated `.expected.svg`, commit both.
3. **Safety net test.** If the feature adds a new fixture directory or changes
   the discovery pattern, verify the `Fixtures_DirectoryIsNotEmpty` guard
   still passes.

---

## 8. Existing Fixture Inventory

| Fixture | Syntax | Diagram type | Key feature exercised |
|---------|--------|-------------|----------------------|
| `conceptual-cycle` | Conceptual | cycle | Back-edge from last to first |
| `conceptual-hierarchy` | Conceptual | hierarchy | Indent-based tree |
| `conceptual-list` | Conceptual | list | Nodes only, no edges |
| `conceptual-matrix` | Conceptual | matrix | Row × column grid |
| `conceptual-process` | Conceptual | process | Linear chain with edges |
| `conceptual-pyramid` | Conceptual | pyramid | Nodes only |
| `conceptual-venn` | Conceptual | venn | Nodes only |
| `mermaid-comments` | Mermaid | flowchart | `%%` comment stripping |
| `mermaid-edge-labels` | Mermaid | flowchart | `\|label\|` syntax on edges |
| `mermaid-edge-styles` | Mermaid | flowchart | Dashed, dotted, thick edges |
| `mermaid-flowchart-lr` | Mermaid | flowchart | Left-to-right direction |
| `mermaid-flowchart-td` | Mermaid | flowchart | Top-down direction |
| `mermaid-mindmap` | Mermaid | mindmap | Indent-based tree |
| `mermaid-node-shapes` | Mermaid | flowchart | Rectangle, rounded, circle, diamond |
| `mermaid-state` | Mermaid | statediagram | `[*]` terminals, state definitions |
| `mermaid-subgraph` | Mermaid | flowchart | `subgraph`/`end` blocks |
