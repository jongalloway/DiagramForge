# Performance Notes

This repository uses a few targeted modern .NET features where they pay off without adding parser complexity:

- System.Text.Json source generation for theme and palette serialization
- generated regular expressions for Mermaid parsers
- lower-allocation line and CSV scanning in parser hot paths

## Benchmarks

Run the BenchmarkDotNet project to measure end-to-end render throughput and allocations for representative diagrams:

```sh
dotnet run -c Release --project benchmarks/DiagramForge.Benchmarks/DiagramForge.Benchmarks.csproj
```

The current benchmark suite covers:

- conceptual 2x2 matrix rendering
- Mermaid flowchart rendering
- Mermaid xychart rendering
