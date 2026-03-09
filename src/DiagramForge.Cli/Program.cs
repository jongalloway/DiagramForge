using DiagramForge;

// DiagramForge CLI
// Usage:
//   diagramforge <input-file> [--output <output-file>]
//   diagramforge --help

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    PrintHelp();
    return 0;
}

string inputPath = args[0];

string? outputPath = null;
for (int i = 1; i < args.Length - 1; i++)
{
    if (args[i] is "--output" or "-o")
    {
        outputPath = args[i + 1];
        break;
    }
}

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
    return 1;
}

try
{
    string diagramText = await File.ReadAllTextAsync(inputPath);
    var renderer = new DiagramRenderer();
    string svg = renderer.Render(diagramText);

    if (outputPath is not null)
    {
        await File.WriteAllTextAsync(outputPath, svg);
        Console.WriteLine($"SVG written to: {outputPath}");
    }
    else
    {
        Console.Write(svg);
    }

    return 0;
}
catch (DiagramForge.Abstractions.DiagramParseException ex)
{
    Console.Error.WriteLine($"Parse error: {ex.Message}");
    if (ex.LineNumber.HasValue)
        Console.Error.WriteLine($"  Line: {ex.LineNumber}");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    return 3;
}

static void PrintHelp()
{
    Console.WriteLine("""
        DiagramForge — Diagram text to SVG renderer

        Usage:
          diagramforge <input-file> [--output <output.svg>]
          diagramforge --help

        Arguments:
          <input-file>          Path to the diagram source file (Mermaid or Conceptual DSL)
          --output, -o <path>   Write SVG to a file instead of stdout

        Supported syntaxes:
          mermaid               Flowchart LR/TB (Mermaid subset)
          conceptual            Process, Cycle, Hierarchy, Venn, List, Matrix, Pyramid

        Examples:
          diagramforge diagram.mmd --output diagram.svg
          diagramforge diagram.txt
        """);
}
