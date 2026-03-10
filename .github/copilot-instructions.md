# DiagramForge Copilot Instructions

This repository prefers first-party tools and MCP integrations over shell commands whenever an MCP can do the job.

## Repository Context

- DiagramForge is a .NET 10 library + CLI with unit tests and SVG snapshot E2E tests.
- The core pipeline is: parser -> semantic model -> layout engine -> SVG renderer.
- Mermaid support is intentionally incremental. Match the documented v1 scope in `doc/prd.md` unless the issue explicitly expands it.

## Tool Selection

- Prefer MCP tools over terminal commands for project operations.
- For .NET work, prefer the Community.Mcp.DotNet / `mcp_dotnet-*` tools before `dotnet` in the shell.
- Use MCPs for build, test, restore, run, format, dependency inspection, EF tasks, workloads, and SDK/environment queries when available.
- Use shell commands only when there is no MCP that covers the task, or when the task is inherently shell-specific.

## .NET Guidance

- Prefer `mcp_dotnet-mcp_dotnet_project` for `restore`, `build`, `test`, `run`, `format`, `clean`, `pack`, and `publish`.
- Prefer the `filter` parameter on the .NET MCP test tool instead of shell-based test filtering.
- Prefer `activate_dotnet_project_management_tools` and `activate_dotnet_sdk_and_server_info_tools` when .NET capabilities are needed.
- If a change needs only targeted test execution, run the smallest filtered test set through the MCP rather than a full shell test run.

## Implementation Guidance

- Prefer small, diagram-specific parser changes over broad cross-cutting rewrites.
- For Mermaid work, follow the existing `Parsers/Mermaid` split by diagram kind instead of growing one monolithic parser.
- Reuse the existing `Diagram`, `Node`, `Edge`, `Group`, `LayoutHints`, and `Theme` model where practical; only extend the model when the existing graph primitives are not enough.
- When adding Mermaid features or diagram types, ground the implementation in both the Mermaid docs and the Mermaid source when needed.
- Keep changes aligned with the issue scope. Implement the minimum accepted subset first and avoid pulling future-scope features into the same change.

## Testing Guidance

- Add or update focused unit tests in `tests/DiagramForge.Tests` for parser, layout, and rendering behavior changed by the work.
- Add or update snapshot fixtures in `tests/DiagramForge.E2ETests/Fixtures` when rendered SVG output changes.
- Use filtered MCP test runs while iterating; only run broader test coverage when the targeted tests are passing.
- Regenerate snapshot baselines only when the SVG change is intentional, then review the generated `.expected.svg` diff before keeping it.

## Practical Rule

Before using a shell command for .NET project work, check whether an MCP tool already provides that capability. If it does, use the MCP.