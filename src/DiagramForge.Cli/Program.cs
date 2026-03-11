using DiagramForge;
using DiagramForge.Models;

// DiagramForge CLI
// Usage:
//   diagramforge <input-file> [--output <output-file>] [--theme <name>] [--palette <json>]
//   diagramforge --help

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    PrintHelp();
    return 0;
}

string inputPath = args[0];

string? outputPath   = ParseFlagValue(args, "--output",  "-o");
string? themeName    = ParseFlagValue(args, "--theme");
string? paletteJson  = ParseFlagValue(args, "--palette");
string? themeFile    = ParseFlagValue(args, "--theme-file");

if (outputPath is null)
{
    int outputIndex = Array.FindIndex(args, 1, arg => arg is "--output" or "-o");
    if (outputIndex >= 0 && outputIndex < args.Length - 1 && !args[outputIndex + 1].StartsWith("-", StringComparison.Ordinal))
        outputPath = args[outputIndex + 1];
}

// Validate value-required flags are not present without a value
if (RequiresFlagValue(args, "--output", "-o") && outputPath is null)
{
    Console.Error.WriteLine("Error: --output/-o flag requires a value.");
    return 1;
}
if (RequiresFlagValue(args, "--theme") && themeName is null)
{
    Console.Error.WriteLine("Error: --theme flag requires a value.");
    return 1;
}
if (RequiresFlagValue(args, "--palette") && paletteJson is null)
{
    Console.Error.WriteLine("Error: --palette flag requires a value.");
    return 1;
}
if (RequiresFlagValue(args, "--theme-file") && themeFile is null)
{
    Console.Error.WriteLine("Error: --theme-file flag requires a value.");
    return 1;
}

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
    return 1;
}

// ── Resolve theme ────────────────────────────────────────────────────────────

Theme? theme = null;

if (themeFile is not null)
{
    if (!File.Exists(themeFile))
    {
        Console.Error.WriteLine($"Error: Theme file not found: {themeFile}");
        return 1;
    }
    try
    {
        string json = await File.ReadAllTextAsync(themeFile);
        theme = Theme.FromJson(json);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"Error: Invalid theme file: {ex.Message}");
        return 1;
    }
}
else if (themeName is not null)
{
    theme = Theme.GetByName(themeName);
    if (theme is null)
    {
        Console.Error.WriteLine($"Error: Unknown theme '{themeName}'. Valid themes: default, dark, neutral, forest, presentation.");
        return 1;
    }
}

// ── Render ───────────────────────────────────────────────────────────────────

try
{
    string diagramText = await File.ReadAllTextAsync(inputPath);
    var renderer = new DiagramRenderer();
    string svg = renderer.Render(diagramText, theme, paletteJson);

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
catch (ArgumentException ex) when (paletteJson is not null)
{
    Console.Error.WriteLine($"Error: Invalid palette JSON: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    return 3;
}

// ── Helpers ──────────────────────────────────────────────────────────────────

static string? ParseFlagValue(string[] args, params string[] flags)
{
    foreach (string flag in flags)
    {
        int idx = Array.FindIndex(args, 1, arg => string.Equals(arg, flag, StringComparison.Ordinal));
        if (idx >= 0 && idx < args.Length - 1 && !args[idx + 1].StartsWith("-", StringComparison.Ordinal))
            return args[idx + 1];
    }
    return null;
}

/// <summary>
/// Returns true when any of the <paramref name="flags"/> are present in <paramref name="args"/>
/// but are not followed by a non-flag value — i.e. the flag is present but its value is missing.
/// </summary>
static bool RequiresFlagValue(string[] args, params string[] flags)
{
    foreach (string flag in flags)
    {
        int idx = Array.FindIndex(args, 1, arg => string.Equals(arg, flag, StringComparison.Ordinal));
        if (idx < 0)
            continue;
        // Flag is present; check whether a value follows it
        if (idx >= args.Length - 1 || args[idx + 1].StartsWith("-", StringComparison.Ordinal))
            return true;
    }
    return false;
}

static void PrintHelp()
{
    Console.WriteLine("""
        DiagramForge — Diagram text to SVG renderer

        Usage:
          diagramforge <input-file> [options]
          diagramforge --help

        Arguments:
          <input-file>                  Path to the diagram source file (Mermaid or Conceptual DSL)

        Options:
          --output, -o <path>           Write SVG to a file instead of stdout
          --theme <name>                Use a built-in named theme:
                                          default, dark, neutral, forest, presentation
          --palette <json>              JSON array of hex colors for node palette, e.g. '["#FF0000","#00FF00"]'
                                        Overrides the node palette of the selected theme.
          --theme-file <path.json>      Load a complete theme from a JSON file.

        Supported syntaxes:
          mermaid                       Flowchart LR/TB (Mermaid subset)
          conceptual                    Matrix, Pyramid

        Examples:
          diagramforge diagram.mmd --output diagram.svg
          diagramforge diagram.mmd --theme dark --output diagram.svg
          diagramforge diagram.mmd --theme dark --palette '["#FF6B6B","#4ECDC4","#45B7D1"]' -o out.svg
          diagramforge diagram.mmd --theme-file mytheme.json -o out.svg
        """);
}
