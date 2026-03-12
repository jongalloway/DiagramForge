using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using DiagramForge;

BenchmarkRunner.Run<RendererBenchmarks>();

[MemoryDiagnoser]
public class RendererBenchmarks
{
    private readonly DiagramRenderer _renderer = new();

    private const string ConceptualMatrix = """
        diagram: matrix
        rows:
          - Important
          - Not Important
        columns:
          - Urgent
          - Not Urgent
        """;

    private const string MermaidFlowchart = """
        flowchart LR
          A[Collect] --> B[Shape]
          B --> C{Review}
          C -->|approved| D[Ship]
          C -->|changes| B
        """;

    private const string MermaidXyChart = """
        xychart-beta
          title "Quarterly Revenue"
          x-axis [Q1, Q2, Q3, Q4]
          y-axis "Revenue" 200 --> 360
          bar [208, 262, 314, 298]
          bar [235, 295, 352, 330]
          line [220, 280, 332, 310]
        """;

    [Benchmark]
    public string RenderConceptualMatrix() => _renderer.Render(ConceptualMatrix);

    [Benchmark]
    public string RenderMermaidFlowchart() => _renderer.Render(MermaidFlowchart);

    [Benchmark]
    public string RenderMermaidXyChart() => _renderer.Render(MermaidXyChart);
}