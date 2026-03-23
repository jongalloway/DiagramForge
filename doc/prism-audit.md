# Prism Theme Audit

> Deliverable for [#128 — Review Prism theme behavior across diagram types](https://github.com/jongalloway/DiagramForge/issues/128).  
> Audit date: 2026-03-23. Reviewed by @copilot.

---

## Prism identity recap

`Theme.CreatePrismTheme()` defines Prism with:

- `NodePalette = ["#FFFFFF", "#FFFFFF", …]` — all eight entries are pure white
- `UseGradients = false` — no fill gradients anywhere
- `UseBorderGradients = true` + `BorderGradientStops = ["#2563EB", "#7C3AED", "#DB2777", "#F59E0B"]` — 4-stop blue→purple→pink→amber rainbow border
- `NodeFillColor = "#FFFFFF"`, `BackgroundColor = "#FFFFFF"` — white canvas

The visual identity is **white fills, expressive rainbow gradient borders, borders carry all the emphasis**.  
Nodes that pass through `SvgRenderSupport.AppendGradientDefs` receive this correctly. Nodes that bypass it — or that use `NodePalette` as direct fill colors on a white background — lose Prism's identity entirely.

---

## Audit method

For each diagram type:

1. Read the `.expected.svg` fixture (or trace the rendering code path) for default, Dracula, and Prism.
2. Confirmed which SVG attributes (`fill`, `stroke`, gradient `<defs>`) are present.
3. Traced the code path in `SvgNodeWriter.AppendNode`, `SvgRenderSupport.AppendGradientDefs`, and diagram-specific layout files.

---

## Category A — Prism renders well ✅

These types route through the standard `AppendGradientDefs` path and receive white fills and the 4-stop rainbow border gradient.

| Diagram type | Verification |
|---|---|
| **Mermaid flowchart** | Confirmed by `mermaid-flowchart-prism.expected.svg` and `mermaid-theme-prism.expected.svg`: all nodes have `fill="#FFFFFF"` and `stroke="url(#node-N-stroke-gradient)"` with 4-stop stops. Groups also use the gradient border. |
| **Mermaid subgraph** | Same standard path; subgraph group border uses `AppendGradientDefs` for stroke. |
| **Mermaid mindmap** | Standard ellipse/rect shape routing — no custom renderer. |
| **Mermaid sequence / state / class / architecture / block / venn / timeline** | All standard shape routing. `AppendClassNode` for UML class still consumes `fill`/`stroke` from `AppendGradientDefs`. |
| **Conceptual chevrons** | `AppendChevronNode` receives `fill`/`stroke` from the standard gradient path — white polygon, rainbow border. |
| **Conceptual funnel** | `AppendFunnelSegmentNode` — same pattern. |
| **Conceptual pyramid** | `AppendPyramidSegmentNode` — same pattern. |
| **Conceptual cycle** | Standard ellipse routing. |
| **Conceptual radial** (spoke nodes) | Standard rect routing for leaf spokes. |
| **Conceptual tree** | Standard rect routing. |

**Root: Prism works wherever a node reaches `SvgRenderSupport.AppendGradientDefs` without a `render:noGradient` override and without its `FillColor` set to an explicit non-white value by the layout.**

---

## Category B — Partially expresses Prism, mutes its identity ⚠️

### Mermaid xychart

**What renders:** Bar fills are accent-derived series colors. For Prism (`AccentColor = "#2563EB"`), bars are shades of blue. Border gradients on the bars **are** the 4-stop Prism rainbow.

**What is muted:** Prism's "white fills, borders carry emphasis" principle is bypassed. Bars look like any other colorful bar chart.

**Root cause:** `SvgRenderSupport.TryResolveXyChartColors` / `GetXyChartSeriesColor` derives 8 accent tints from `theme.AccentColor`, bypassing `theme.NodePalette` and `theme.NodeFillColor` entirely. Because `UseGradients = false` in Prism, the bar fill is the raw accent-derived solid color. The 4-stop border gradient is present, which partially expresses Prism, but the colored bar fills undermine the restrained-fill language.

**Severity:** Medium. Bar charts need series colors for data legibility; this is partly a design question. A follow-up should decide whether Prism should intentionally degrade series colors, apply a lighter tint, or explicitly opt xycharts out of the white-fill rule.

---

## Category C — Largely bypasses Prism-specific styling 🚨

### Conceptual target

**What renders:** Ring nodes get solid palette-derived colors from `ThemePaletteResolver.BuildRingColors`. The center node gets a `PrimaryColor`/`TextColor` blend. Card sidebar nodes get background/surface blends with solid single-color strokes.

**Root cause (code references):**

- `DefaultLayoutEngine.Target.cs` line 107: `centerNode.Metadata["render:noGradient"] = true`
- `DefaultLayoutEngine.Target.cs` line 139: `ringNode.Metadata["render:noGradient"] = true`
- `AppendTargetRingNode` (SvgNodeWriter.cs:352–371): receives pre-computed `stroke` as a plain string and draws a circle with that solid stroke — never uses `theme.BorderGradientStops`.
- `AppendTargetCardNode` (SvgNodeWriter.cs:373–419): renders the inner accent border using the pre-computed `stroke` as a solid color.
- `ThemePaletteResolver.BuildRingColors` derives vivid semantic colors from `theme.AccentColor`, `theme.SecondaryColor`, etc. These are vivid in every theme — the Prism all-white `NodePalette` has no effect because ring colors are derived from semantic (non-palette) sources.

**Severity:** High. Target is a prominent conceptual type. The diagram is theme-aware (semantic colors react to Prism values) but expresses zero Prism-specific visual identity (no white fills, no rainbow border gradients).

---

### Conceptual snake

**What renders:** The snake tube gradient and the circular node fills both use `theme.NodePalette` as direct fill colors. In Prism every NodePalette entry is `#FFFFFF`.

- Node circles: `FillColor = palette[i]` = `#FFFFFF` → white circles on white background — invisible.
- Snake path: `segmentColors = palette[i]` = `#FFFFFF` → white tube on white background — the entire serpentine path disappears.
- Outline stroke is `theme.BackgroundColor` (`#FFFFFF`) — no contrast at all.

**Root cause (code references):**

- `DefaultLayoutEngine.Snake.cs` lines 145–151: `string[] palette = theme.NodePalette is { Count: > 0 } ? [.. theme.NodePalette] : [theme.NodeFillColor]`
- Line 170: `node.FillColor = palette[i]`
- Lines 213–220: `segmentColors.Add(palette[i % palette.Length])`
- No check for whether every palette entry matches the background color.

**Severity:** Critical. A Prism snake diagram is a blank white rectangle.

---

### Conceptual tablist — all three variants (cards, stacked, flat)

**What renders:** All three tablist variants call `render:noGradient = true` and set their accent/tab/bar fills from `ColorUtils.Vibrant(palette[i], 2.x)`. In Prism `palette[i] = "#FFFFFF"` and `Vibrant` of pure white (zero saturation) returns white unchanged.

| Variant | Element | Prism fill value | Visual result |
|---|---|---|---|
| Cards | Accent block (left column) | `Vibrant("#FFFFFF", 2.5)` = `#FFFFFF` | Invisible on white |
| Cards | Content card area | `Lighten("#FFFFFF", 0.88)` = `#FFFFFF` | Invisible on white |
| Stacked | Number tab block | `Vibrant("#FFFFFF", 2.5)` = `#FFFFFF` | Invisible on white |
| Stacked | Content bar | `Blend(Vibrant("#FFFFFF", 1.8), "#FFFFFF", 0.35)` = `#FFFFFF` | Invisible on white |
| Flat | Vertical accent line | `tablist:accentColor = Vibrant(palette[0], 2.5)` = `#FFFFFF` | Invisible on white |
| Flat | Title bar | `Vibrant("#FFFFFF", 2.0)` = `#FFFFFF` | Invisible on white |

Additionally, all three variants set `render:noGradient = true` (cards: TabList.cs line 140, stacked: line 248, flat: line 357), which bypasses even the border-gradient path — the 4-stop rainbow gradient is also suppressed.

**Root cause (code references):**

- `DefaultLayoutEngine.TabList.cs` lines 113–115, 217–219, 327–329: all three layout methods use `theme.NodePalette` as base palette.
- All three call `ColorUtils.Vibrant(catFill, 2.x)` where `catFill = palette[i] = "#FFFFFF"`.
- All three set `render:noGradient = true`.

**Severity:** Critical. A Prism tablist of any variant is entirely invisible — no structural chrome, no readable sections.

---

## Summary table

| Diagram type | Prism fidelity | Root cause |
|---|---|---|
| Mermaid flowchart | **A** ✅ | Standard gradient path |
| Mermaid subgraph | **A** ✅ | Standard gradient path |
| Mermaid mindmap | **A** ✅ | Standard shape routing |
| Mermaid sequence / state / class / arch / block / venn | **A** ✅ | Standard shape routing |
| Conceptual chevrons / funnel / pyramid | **A** ✅ | Custom polygon shape; uses standard fill/stroke |
| Conceptual cycle / radial / tree | **A** ✅ | Standard routing |
| **Mermaid xychart** | **B** ⚠️ | `TryResolveXyChartColors` uses accent-tint palette, not white fills; border gradient present |
| **Conceptual target** | **C** 🚨 | `render:noGradient=true` + `BuildRingColors` bypasses Prism entirely |
| **Conceptual snake** | **C** 🚨 | NodePalette all-white → invisible tube + invisible circles |
| **Conceptual tablist (all 3 variants)** | **C** 🚨 | `render:noGradient=true` + `Vibrant(white)` = white on white |

---

## Answering the audit questions

**Which diagram types already show Prism's intended white-plus-gradient-border language?**  
All Mermaid types (flowchart, subgraph, mindmap, class, sequence, state, architecture, block, venn, timeline) and the simpler conceptual types (chevrons, funnel, pyramid, cycle, radial spokes, tree). These all route through `AppendGradientDefs` unchanged.

**Which diagram types only react to Prism through semantic color changes while suppressing Prism's distinctive styling?**  
Conceptual target — it uses Prism's semantic colors (AccentColor, PrimaryColor, etc.) to derive ring/card colors, so it changes appearance between themes, but the white-fill and rainbow-gradient-border features are entirely absent.

**Are there categories of diagrams where Prism should intentionally degrade?**  
Possibly: xychart bars arguably need series color differentiation to encode data. A deliberate opt-out could be documented as intentional. Wireframe diagrams are intentionally theme-agnostic by design and are a justified exception. All other current Category C cases look like unintentional regressions.

**Where should DiagramForge standardize theme participation versus allowing diagram-specific exceptions?**  
- `render:noGradient=true` is a valid exception mechanism for nodes that need saturated fills (e.g., pillars title nodes in non-Prism themes), but it should not be applied to nodes whose fill is palette-derived without a fallback for monochrome palettes.
- Snake / tablist should detect when NodePalette is monochrome (all entries the same color, or all matching the background) and substitute meaningful accent colors.
- Target ring nodes should offer a Prism-native rendering mode that uses `theme.BorderGradientStops` for the ring stroke gradient.

---

## Proposed follow-up issues

The following issues should be filed as `enhancement` with the listed root cause and fix direction. They are ordered by severity.

### Issue 1 — Conceptual snake: invisible rendering in Prism (Critical)

**Root cause:** `DefaultLayoutEngine.Snake.cs` assigns `node.FillColor = palette[i]` and `segmentColors.Add(palette[i])` from `theme.NodePalette`. In Prism every palette entry is `#FFFFFF`, making both node circles and the snake tube invisible on the white background.

**Fix direction:**
- In the snake layout, detect when the resolved palette is monochrome or matches `theme.BackgroundColor`.
- When detected, fall back to `theme.BorderGradientStops` as segment colors (mapping evenly across nodes), or derive vibrant colors from `theme.AccentColor` / `theme.SecondaryColor` via `ThemePaletteResolver`.
- Ensure the snake path outline stroke uses a dark neutral rather than always `theme.BackgroundColor` when the background and path colors match.

---

### Issue 2 — Conceptual tablist: invisible in Prism (Critical)

**Root cause:** All three layout variants (`DefaultLayoutEngine.TabList.cs`) derive accent/tab/bar fills from `ColorUtils.Vibrant(palette[i], 2.x)` where `palette[i] = "#FFFFFF"` in Prism. `Vibrant` of pure white has no effect. Additionally, all three set `render:noGradient = true`, suppressing even the border-gradient path.

**Fix direction:**
- Detect the monochrome-palette case in the tablist layout (or in `ColorUtils.Vibrant`): when the input color is achromatic (S ≈ 0), substitute the first `theme.BorderGradientStop` (or `theme.AccentColor`) as the accent base.
- Alternatively, replace the direct `palette[i]` lookup with a dedicated `ThemePaletteResolver` helper that handles monochrome palettes gracefully.
- The `render:noGradient = true` flag is applied to prevent double-gradient on an already-filled accent block; consider whether it should be replaced with a more surgical per-element gradient opt-in.

---

### Issue 3 — Conceptual target: Prism identity bypassed (High)

**Root cause:** Ring and center nodes use `render:noGradient = true` with explicitly set `FillColor`/`StrokeColor` derived by `ThemePaletteResolver.BuildRingColors`. `AppendTargetRingNode` draws a plain `circle` with a solid `stroke` color; `AppendTargetCardNode` uses `stroke` as a solid color for the inner accent border. Neither path consults `theme.BorderGradientStops`.

**Fix direction:**
- `AppendTargetRingNode`: when `theme.UseBorderGradients && theme.BorderGradientStops is { Count: > 1 }`, emit a `<linearGradient>` for the ring stroke using the gradient stops and reference it as `stroke="url(#...)"`.
- `AppendTargetCardNode`: similarly, use a gradient for the inner accent border rect stroke.
- The center node's solid fill is intentional (it needs contrast against the rings), so keep `render:noGradient = true` there but consider using the first `BorderGradientStop` as the fill color for Prism.

---

### Issue 4 — Mermaid xychart: bar fills bypass Prism white-fill strategy (Medium)

**Root cause:** `SvgRenderSupport.GetXyChartSeriesColor` derives an 8-color palette from `theme.AccentColor` tints, completely bypassing `theme.NodePalette`. In Prism, bars receive vivid blue fills (accent-derived) rather than white, undermining Prism's restrained-fill identity.

**Fix direction:**
- Decide the policy: should xychart bars in Prism be white (consistent with node fills) or should they use lighter tints of `BorderGradientStops` for series encoding?
- A practical middle ground: when `theme.UseGradients == false && theme.UseBorderGradients == true && theme.BorderGradientStops is { Count: > 1 }`, build the series palette from `theme.BorderGradientStops` with appropriate lightening rather than from the accent tint ladder. This preserves data encoding while using Prism's characteristic colors.
- Lower bar fill opacity (already at 0.88 for light backgrounds) could also help soften the filled appearance for Prism.
