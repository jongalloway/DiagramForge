using System.Globalization;
using System.Text.RegularExpressions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

/// <summary>
/// Parses Mermaid <c>xychart-beta</c> diagrams into the semantic model.
/// </summary>
/// <remarks>
/// Supported syntax (v1 subset):
/// <list type="bullet">
///   <item><c>xychart-beta</c> header</item>
///   <item>Optional <c>title "…"</c></item>
///   <item><c>x-axis</c> with category labels <c>[a, b, c]</c> or numeric range <c>min --&gt; max</c></item>
///   <item><c>y-axis</c> with optional label and optional <c>min --&gt; max</c> range</item>
///   <item><c>bar [v1, v2, …]</c> data series (multiple allowed)</item>
///   <item><c>line [v1, v2, …]</c> data series (multiple allowed)</item>
/// </list>
/// </remarks>
internal sealed partial class MermaidXyChartParser : IMermaidDiagramParser
{
    [GeneratedRegex(@"\[([^\]]*)\]", RegexOptions.CultureInvariant)]
    private static partial Regex BracketListRegex();

    [GeneratedRegex(@"([\d.]+)\s*-->\s*([\d.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex NumericRangeRegex();

    [GeneratedRegex(@"""([^""]*)""", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedStringRegex();

    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.XyChart;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("xychart");

        string[]? xCategories = null;
        double? xMin = null, xMax = null;
        string? yLabel = null;
        double? yMin = null, yMax = null;
        var barSeries = new List<double[]>();
        var lineSeries = new List<double[]>();

        // Skip index 0 (the "xychart-beta" header).
        for (int i = 1; i < document.Lines.Length; i++)
        {
            var line = document.Lines[i];

            // title "…" or title …
            if (line.StartsWith("title ", StringComparison.OrdinalIgnoreCase))
            {
                var titleText = line["title ".Length..].Trim();
                var quoted = QuotedStringRegex().Match(titleText);
                builder.WithTitle(quoted.Success ? quoted.Groups[1].Value : titleText);
                continue;
            }

            // x-axis [jan, feb, mar] or x-axis "Label" [jan, feb, mar] or x-axis min --> max
            if (line.StartsWith("x-axis", StringComparison.OrdinalIgnoreCase))
            {
                var rest = line["x-axis".Length..].Trim();
                var bracketMatch = BracketListRegex().Match(rest);
                if (bracketMatch.Success)
                {
                    xCategories = ParseStringList(bracketMatch.Groups[1].Value);
                }
                else
                {
                    var rangeMatch = NumericRangeRegex().Match(rest);
                    if (rangeMatch.Success)
                    {
                        xMin = ParseDouble(rangeMatch.Groups[1].Value);
                        xMax = ParseDouble(rangeMatch.Groups[2].Value);
                    }
                }
                continue;
            }

            // y-axis "Revenue (in $)" 4000 --> 11000
            if (line.StartsWith("y-axis", StringComparison.OrdinalIgnoreCase))
            {
                var rest = line["y-axis".Length..].Trim();
                var quotedMatch = QuotedStringRegex().Match(rest);
                if (quotedMatch.Success)
                {
                    yLabel = quotedMatch.Groups[1].Value;
                    rest = rest[(quotedMatch.Index + quotedMatch.Length)..].Trim();
                }
                var rangeMatch = NumericRangeRegex().Match(rest);
                if (rangeMatch.Success)
                {
                    yMin = ParseDouble(rangeMatch.Groups[1].Value);
                    yMax = ParseDouble(rangeMatch.Groups[2].Value);
                }
                continue;
            }

            // bar [5000, 6000, 7500]
            if (line.StartsWith("bar", StringComparison.OrdinalIgnoreCase))
            {
                var bracketMatch = BracketListRegex().Match(line);
                if (bracketMatch.Success)
                    barSeries.Add(ParseDoubleList(bracketMatch.Groups[1].Value));
                continue;
            }

            // line [5000, 6000, 7000]
            if (line.StartsWith("line", StringComparison.OrdinalIgnoreCase))
            {
                var bracketMatch = BracketListRegex().Match(line);
                if (bracketMatch.Success)
                    lineSeries.Add(ParseDoubleList(bracketMatch.Groups[1].Value));
                continue;
            }
        }

        // Determine category count from the data or axis definition.
        int categoryCount = xCategories?.Length ?? 0;
        if (categoryCount == 0)
        {
            foreach (var s in barSeries)
                categoryCount = Math.Max(categoryCount, s.Length);
            foreach (var s in lineSeries)
                categoryCount = Math.Max(categoryCount, s.Length);
        }

        // Generate default category labels if only a numeric range was given.
        xCategories ??= GenerateNumericLabels(categoryCount, xMin, xMax);

        // Auto-compute Y range from data if not specified.
        bool hasValues = false;
        double maxValue = 0;
        foreach (var series in barSeries)
        {
            for (int i = 0; i < series.Length; i++)
            {
                if (!hasValues || series[i] > maxValue)
                    maxValue = series[i];
                hasValues = true;
            }
        }

        foreach (var series in lineSeries)
        {
            for (int i = 0; i < series.Length; i++)
            {
                if (!hasValues || series[i] > maxValue)
                    maxValue = series[i];
                hasValues = true;
            }
        }

        if (hasValues)
        {
            yMin ??= 0;
            yMax ??= maxValue * 1.1; // 10% headroom
        }
        else
        {
            yMin ??= 0;
            yMax ??= 100;
        }

        // Store chart metadata for layout and rendering.
        var diagram = builder.Build();
        diagram.Metadata["xychart:categoryCount"] = categoryCount;
        diagram.Metadata["xychart:categories"] = xCategories;
        diagram.Metadata["xychart:yMin"] = yMin.Value;
        diagram.Metadata["xychart:yMax"] = yMax.Value;
        if (yLabel is not null)
            diagram.Metadata["xychart:yLabel"] = yLabel;

        // Create bar nodes — one per data point per series.
        int seriesIndex = 0;
        foreach (var series in barSeries)
        {
            for (int ci = 0; ci < series.Length && ci < categoryCount; ci++)
            {
                var id = $"bar_{seriesIndex}_{ci}";
                var node = new Node(id, string.Empty) { Shape = Shape.Rectangle };
                node.Metadata["xychart:kind"] = "bar";
                node.Metadata["xychart:paletteIndex"] = seriesIndex;
                node.Metadata["xychart:seriesIndex"] = seriesIndex;
                node.Metadata["xychart:categoryIndex"] = ci;
                node.Metadata["xychart:value"] = series[ci];
                diagram.AddNode(node);
            }
            seriesIndex++;
        }

        // Create line-point nodes — one per data point per series.
        int lineSeriesIndex = 0;
        foreach (var series in lineSeries)
        {
            for (int ci = 0; ci < series.Length && ci < categoryCount; ci++)
            {
                var id = $"line_{lineSeriesIndex}_{ci}";
                var node = new Node(id, string.Empty) { Shape = Shape.Circle };
                node.Metadata["xychart:kind"] = "linePoint";
                node.Metadata["xychart:paletteIndex"] = barSeries.Count + lineSeriesIndex;
                node.Metadata["xychart:seriesIndex"] = lineSeriesIndex;
                node.Metadata["xychart:categoryIndex"] = ci;
                node.Metadata["xychart:value"] = series[ci];
                diagram.AddNode(node);
            }
            lineSeriesIndex++;
        }

        // Store series counts for rendering.
        diagram.Metadata["xychart:barSeriesCount"] = barSeries.Count;
        diagram.Metadata["xychart:lineSeriesCount"] = lineSeries.Count;

        return diagram;
    }

    private static string[] ParseStringList(string csv)
    {
        var values = new List<string>();
        var text = csv.AsSpan();
        int start = 0;
        while (start <= text.Length)
        {
            int length = 0;
            while (start + length < text.Length && text[start + length] != ',')
                length++;

            var part = text.Slice(start, length);
            var value = part.Trim().Trim('"');
            if (!value.IsEmpty)
                values.Add(value.ToString());

            start += length + 1;
            if (start > text.Length)
                break;
        }

        return [.. values];
    }

    private static double[] ParseDoubleList(string csv)
    {
        var values = new List<double>();
        var text = csv.AsSpan();
        int start = 0;
        while (start <= text.Length)
        {
            int length = 0;
            while (start + length < text.Length && text[start + length] != ',')
                length++;

            var part = text.Slice(start, length);
            var value = part.Trim();
            if (!value.IsEmpty)
                values.Add(ParseDouble(value));

            start += length + 1;
            if (start > text.Length)
                break;
        }

        return [.. values];
    }

    private static double ParseDouble(ReadOnlySpan<char> s)
    {
        return double.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static string[] GenerateNumericLabels(int count, double? min, double? max)
    {
        if (count == 0) return [];
        if (min is not null && max is not null && count > 0)
        {
            double step = (max.Value - min.Value) / Math.Max(1, count - 1);
            return Enumerable.Range(0, count)
                .Select(i => (min.Value + i * step).ToString("G", CultureInfo.InvariantCulture))
                .ToArray();
        }
        return Enumerable.Range(1, count).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();
    }

}
