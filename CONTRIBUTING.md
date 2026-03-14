# Contributing to DiagramForge

## Prerequisites

- **.NET 10 SDK** (`10.0.100` or later). The repo's [`global.json`](global.json) sets `10.0.100` as the floor with `rollForward: latestFeature` and `allowPrerelease: true`, so newer 10.x SDKs (including previews) work.
- That's it. No other toolchain, no native dependencies.

Check your SDK:

```sh
dotnet --version
```

## Clone & build

```sh
git clone https://github.com/jongalloway/DiagramForge.git
cd DiagramForge
dotnet build
```

The solution file is [`DiagramForge.slnx`](DiagramForge.slnx) (XML-based `.slnx` format). `dotnet build` at the repo root finds it automatically.

## Test

```sh
dotnet test
```

Runs both test projects:

| Project                       | What it covers                                                               |
| ----------------------------- | ---------------------------------------------------------------------------- |
| `tests/DiagramForge.Tests`    | Unit tests — parsers, layout engine, SVG renderer, models.                   |
| `tests/DiagramForge.E2ETests` | End-to-end snapshot tests — full `text → SVG` pipeline against golden files. |

Both use **xUnit v3** with **Microsoft Testing Platform (MTP)**. Test projects compile to self-contained executables (`<OutputType>Exe</OutputType>`); `dotnet test` routes through MTP via `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>`. No VSTest adapter, no `xunit.runner.visualstudio`.

### Snapshot tests

The E2E suite globs `tests/DiagramForge.E2ETests/Fixtures/*.input`, renders each one, and compares against the neighbouring `*.expected.svg` using XML-canonical comparison (whitespace and attribute order don't matter).

Every run also writes `artifacts/test-results/snapshots/*.actual.svg` — pass or fail — so you can open the rendered output directly. The `artifacts/` directory is gitignored.

**When a snapshot fails and the new output is correct,** regenerate the baseline:

```sh
# PowerShell
$env:UPDATE_SNAPSHOTS = '1'; dotnet test tests/DiagramForge.E2ETests; Remove-Item Env:UPDATE_SNAPSHOTS

# bash
UPDATE_SNAPSHOTS=1 dotnet test tests/DiagramForge.E2ETests
```

This writes new `.expected.svg` files directly into the **source** `Fixtures/` directory (the test locates the repo root by walking up to the `.slnx`). Review the diff and commit the baselines alongside the code change that caused them.

`UPDATE_SNAPSHOTS` is force-set to `0` in CI — you can't accidentally auto-pass a broken PR.

**Adding a new fixture:** drop a `whatever.input` file in `Fixtures/`, run with `UPDATE_SNAPSHOTS=1` to generate its baseline, commit both.

## Project layout

```text
DiagramForge.slnx              solution (XML .slnx format)
Directory.Build.props          shared MSBuild properties (TFM, nullable, etc.)
Directory.Packages.props       central package versions
global.json                    SDK floor

src/
  DiagramForge/                the library
    DiagramRenderer.cs         public entry point — text → SVG
    Abstractions/              IDiagramParser, ILayoutEngine, ISvgRenderer, ...
    Models/                    Diagram, Node, Edge, Theme, LayoutHints, ...
    Parsers/
      Mermaid/                 flowchart subset
      Conceptual/              process/cycle/hierarchy/venn/list/matrix/pyramid
    Layout/                    DefaultLayoutEngine — layered/BFS positioning
    Rendering/                 SvgRenderer — emits SVG XML

  DiagramForge.Cli/            thin console wrapper around DiagramRenderer

tests/
  DiagramForge.Tests/          unit tests
  DiagramForge.E2ETests/       snapshot tests
    Fixtures/                  *.input + *.expected.svg pairs

.github/workflows/ci.yml       restore → build → test, uploads TRX + rendered SVGs
```

## Conventions

### Central Package Management

NuGet versions live **only** in `Directory.Packages.props`. Project files reference packages by name with no `Version` attribute.

To add a package to a project:

1. Add `<PackageVersion Include="Foo" Version="1.2.3" />` to `Directory.Packages.props`
2. Add `<PackageReference Include="Foo" />` (no version) to the `.csproj`

Permissive-OSS licenses only — MIT preferred, Apache-2.0 acceptable.

### Local package troubleshooting

If local testing behaves differently from the published NuGet package, check whether a stale local package source or cache is overriding the real package.

- Clear caches with `dotnet nuget locals all --clear`
- Remove stale package folders such as `%USERPROFILE%\.nuget\packages\diagramforge\1.0.0`
- Be careful with local sources like `artifacts/nupkg`, `--add-source`, or custom `NuGet.Config` entries
- If needed, validate the published package explicitly with `RestoreSources=https://api.nuget.org/v3/index.json` and an isolated `RestorePackagesPath`

Matching package ID and version do not guarantee matching bits. When diagnosing package issues, compare package or DLL hashes before assuming the published package is wrong.

### Adding a parser

Implement `IDiagramParser`:

```csharp
public sealed class MyParser : IDiagramParser
{
    public string SyntaxId => "mysyntax";
    public bool CanParse(string text) => /* sniff first line */;
    public Diagram Parse(string text)  => /* build via DiagramSemanticModelBuilder */;
}
```

Register it in `DiagramRenderer`'s default constructor (or leave it opt-in via `RegisterParser`). Add unit tests under `tests/DiagramForge.Tests/Parsers/` and at least one E2E fixture.

### Layout / rendering changes

Anything that moves pixels will shift snapshot baselines. Before opening a PR:

1. Run `dotnet test` — see which snapshots fail
2. **Look at the actual count.** If all 13 moved for a one-line fix, something's off.
3. Open `artifacts/test-results/snapshots/*.actual.svg` in a browser and eyeball the change
4. Regenerate with `UPDATE_SNAPSHOTS=1`
5. Explain the baseline delta in the PR description

## Submitting a PR

- Branch from `main`
- Tests green locally (`dotnet test` at root)
- CI must pass — it builds Release, runs all tests, and uploads rendered SVGs as artifacts for review
- Baseline changes: call out how many moved and why
