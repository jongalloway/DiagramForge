# Product Requirements Document (PRD)
## Project: Markdown‑Driven Diagram Rendering Engine (Text → SVG)

---

## 1. Overview

This project provides a **standalone, pluggable, text‑to‑SVG diagram rendering engine**.  
It accepts **diagram source text** (e.g., Mermaid, Conceptual DSL, future formats) and outputs **high‑quality SVG**.
It is built in .NET 10 and includes a library and a CLI runner that can be invoked via `dnx`.

The engine is:

- **Markdown‑friendly** (but not tied to any Markdown processor)
- **Format‑agnostic** (input is just diagram text)
- **Client‑agnostic** (Marp, static site generators, PPTX converters, editors, etc.)
- **Extensible** (pluggable parsers for new diagram syntaxes)
- **Focused on modern, slide‑friendly aesthetics**

The engine does **not** parse Markdown itself.  
Clients (e.g., MarpToPptx, Obsidian plugins, CLI tools) are responsible for extracting diagram text blocks and passing them to this renderer.

---

## 2. Goals & Non‑Goals

### 2.1 Goals

- Provide a **clean API**: input = diagram text, output = SVG.
- Support a **subset of Mermaid** as the initial diagram syntax.
- Introduce a **Conceptual Diagram DSL** for SmartArt‑style conceptual diagrams.
- Normalize all diagram inputs into a **unified semantic model**.
- Render diagrams as **modern, clean SVGs** with:
  - Rounded corners
  - Modern palettes
  - Consistent spacing
  - Slide‑friendly proportions
- Provide a **pluggable parser architecture** for future syntaxes (e.g., D2, DOT).
- Remain **independent of any specific client** (Marp, PPTX, web apps, etc.).
- Support for themes and palletes, both built-in and user supplied.
- Leverage existing ecosystem. Refer to https://github.com/mermaid-js/mermaid for specification and transpile open source code (with attribution) if possible.

### 2.2 Non‑Goals

- Full Mermaid feature parity.
- D2 support in v1.
- Native PowerPoint chart generation (handled by other systems).
- Markdown parsing or extraction of code blocks.
- Browser‑based rendering or JS runtime dependency.
- Reproducing SmartArt exactly; instead, provide modern equivalents.

---

## 3. Target Users

- Developers embedding diagrams in documentation or slide pipelines.
- Tools that need deterministic, theme‑able SVG diagrams.
- Markdown‑based workflows (Marp, MkDocs, Obsidian, static site generators).
- Presentation pipelines that need conceptual diagrams without manual editing.

---

## 4. High‑Level Architecture

    Diagram Text (Mermaid, Conceptual DSL, etc.)
       ↓
    Parser (pluggable)
       ↓
    Unified Semantic Diagram Model
       ↓
    Layout Engine (slide‑optimized)
       ↓
    SVG Renderer (.NET)
       ↓
    Output: SVG string or stream

### 4.1 Pluggable Parser Architecture

Each parser implements:

- IDiagramParser
- IDiagramSemanticModelBuilder

Parsers convert raw text → semantic model.  
Renderers never depend on specific syntaxes.

### 4.2 Unified Semantic Diagram Model

Core primitives:

- Diagram
- Node
- Edge
- Group / Container
- Label
- Shape (rectangle, circle, pill, diamond, etc.)
- LayoutHints (direction, spacing, alignment)
- Theme (colors, fonts, border radius)

### 4.3 Rendering Engine

- Deterministic layout
- Modern design defaults
- Theme‑driven styling
- SVG output only
- No browser engines or JS runtimes

---

## 5. Supported Diagram Types (v1)

### 5.1 Mermaid Subset

- Flowchart (LR, TB)
- Hierarchy (flowchart or mindmap subset)
- Timeline
- Block diagram
- Simple relationship diagrams

### 5.2 Conceptual Diagram DSL

SmartArt‑inspired conceptual diagrams:

- Process (linear, segmented, vertical, horizontal)
- Hierarchy (org‑chart style)
- Cycle (radial, segmented)
- Relationship (venn, overlapping sets, arrows between concepts)
- List (bulleted, stacked, picture‑accent)
- Matrix (2×2, 3×3)
- Pyramid (segmented, labeled)

### 5.3 Future Diagram Types (not in v1)

- D2 subset
- Architecture diagrams
- Network diagrams
- Swimlanes
- Gantt charts

---

## 6. Conceptual Diagram DSL (Draft)

### 6.1 Example: Process

    diagram: process
    steps:
      - Discover
      - Plan
      - Build
      - Test
      - Deploy

### 6.2 Example: Cycle

    diagram: cycle
    items:
      - Plan
      - Build
      - Test
      - Deploy

### 6.3 Example: Hierarchy

    diagram: hierarchy
    CEO:
      - CTO:
          - Engineering
      - CFO:
          - Finance

### 6.4 Example: Relationship (Venn)

    diagram: venn
    sets:
      - Engineering
      - Product
      - Design

---

## 7. Theming & Styling

### 7.1 Defaults

- Rounded corners
- Modern color palettes
- Inter or Segoe UI font
- Consistent spacing
- Slide‑friendly proportions

### 7.2 Theme Overrides

Users can override:

- Colors
- Border radius
- Spacing
- Font family
- Node shapes

---

## 8. Integration Model (Client‑Agnostic)

This engine does **not** parse Markdown.

Clients are responsible for:

- Extracting diagram text blocks
- Determining diagram type (e.g., "mermaid", "diagram")
- Passing raw text to the renderer
- Embedding the resulting SVG

Example clients:

- MarpToPptx
- Static site generators
- CLI tools
- Editors (VS Code, Obsidian)
- Web apps

---

## 9. Roadmap

### Phase 1 (MVP)

- Mermaid subset parser
- Conceptual DSL parser
- Unified semantic model
- Basic layout engine
- SVG renderer
- Public API: string → SVG

### Phase 2

- Advanced conceptual diagram types
- Theme packs
- Layout tuning for slide aesthetics
- Plugin API for additional syntaxes

### Phase 3

- Optional D2 parser
- Optional DOT parser
- Optional natural‑language → diagram compiler

---

## 10. Risks & Mitigations

| Risk                          | Mitigation                                  |
|-------------------------------|---------------------------------------------|
| Mermaid syntax complexity     | Support a well‑defined subset               |
| Conceptual DSL scope creep    | Start with core SmartArt categories         |
| Layout engine complexity      | Begin with simple deterministic layouts     |
| SVG rendering inconsistencies | Build a strict test suite with golden SVGs  |
| Future syntax integration     | Pluggable parser architecture               |

---

## 11. Success Criteria

- Input text reliably produces clean SVG output.
- SVGs look modern and professional.
- Mermaid subset renders correctly.
- Conceptual DSL covers core SmartArt‑style diagrams.
- Architecture supports future syntaxes without refactoring.
