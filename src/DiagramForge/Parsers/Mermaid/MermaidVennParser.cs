using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidVennParser : IMermaidDiagramParser
{
    private static readonly string[] DefaultSetFillPalette =
    [
        "#4F81BD",
        "#70AD47",
        "#ED7D31",
    ];

    private static readonly string[] DefaultSetStrokePalette =
    [
        "#4F81BD",
        "#70AD47",
        "#ED7D31",
    ];

    private const double DefaultSetFillOpacity = 0.40;

    private const string FillOpacityMetadataKey = "render:fillOpacity";
    private const string RenderTextOnlyMetadataKey = "render:textOnly";
    private const string VennKindMetadataKey = "venn:kind";
    private const string VennParentSetMetadataKey = "venn:parentSet";
    private const string VennRegionMetadataKey = "venn:region";
    private const string VennTextIndexMetadataKey = "venn:textIndex";

    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.VennDiagram;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("venn")
            .WithLayoutHints(new LayoutHints { Direction = LayoutDirection.LeftToRight });

        var diagram = builder.Build();
        var declaredSets = new List<(string Id, Node Node)>();
        var setIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        var setNodesById = new Dictionary<string, Node>(StringComparer.Ordinal);
        var textNodesById = new Dictionary<string, Node>(StringComparer.Ordinal);
        var overlapNodesByRegion = new Dictionary<string, Node>(StringComparer.Ordinal);
        IndentedTextTarget? currentTextTarget = null;

        for (int i = 1; i < document.RawLines.Length; i++)
        {
            string rawLine = document.RawLines[i];
            int indent = CountIndent(rawLine);
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            if (currentTextTarget is { } textTarget && indent > textTarget.Indent)
            {
                if (!line.StartsWith("text ", StringComparison.Ordinal))
                    throw new DiagramParseException($"Unsupported Mermaid venn nested statement: '{line}'.");

                var textDeclaration = ParseTextDeclaration(line["text ".Length..]);
                if (diagram.Nodes.ContainsKey(textDeclaration.Id))
                    throw new DiagramParseException($"Duplicate Mermaid venn node '{textDeclaration.Id}'.");

                var textNode = new Node(textDeclaration.Id, textDeclaration.Label ?? textDeclaration.Id);
                textNode.Metadata[VennKindMetadataKey] = "text";
                textNode.Metadata[RenderTextOnlyMetadataKey] = true;
                textNode.Metadata[VennTextIndexMetadataKey] = textTarget.NextTextIndex;

                if (textTarget.Kind == VennTextTargetKind.Set)
                    textNode.Metadata[VennParentSetMetadataKey] = textTarget.Key;
                else
                    textNode.Metadata[VennRegionMetadataKey] = textTarget.Key;

                diagram.AddNode(textNode);
                textNodesById[textNode.Id] = textNode;
                currentTextTarget = textTarget with { NextTextIndex = textTarget.NextTextIndex + 1 };
                continue;
            }

            currentTextTarget = null;

            if (line.StartsWith("title ", StringComparison.Ordinal))
            {
                diagram.Title = Unquote(line["title ".Length..].Trim());
                continue;
            }

            if (line.StartsWith("set ", StringComparison.Ordinal))
            {
                var declaration = ParseEntityDeclaration(line["set ".Length..]);
                if (setIndices.ContainsKey(declaration.Id))
                    throw new DiagramParseException($"Duplicate Mermaid venn set '{declaration.Id}'.");
                if (diagram.Nodes.ContainsKey(declaration.Id))
                    throw new DiagramParseException($"Duplicate Mermaid venn node '{declaration.Id}'.");

                var node = new Node(declaration.Id, declaration.Label)
                {
                    Shape = Shape.Circle,
                    FillColor = DefaultSetFillPalette[declaredSets.Count % DefaultSetFillPalette.Length],
                    StrokeColor = DefaultSetStrokePalette[declaredSets.Count % DefaultSetStrokePalette.Length],
                };
                node.Metadata[FillOpacityMetadataKey] = DefaultSetFillOpacity;
                node.Metadata["venn:index"] = declaredSets.Count;
                diagram.AddNode(node);
                setIndices[declaration.Id] = declaredSets.Count;
                setNodesById[declaration.Id] = node;
                declaredSets.Add((declaration.Id, node));
                currentTextTarget = new IndentedTextTarget(VennTextTargetKind.Set, declaration.Id, indent, 0);
                continue;
            }

            if (line.StartsWith("union ", StringComparison.Ordinal))
            {
                if (declaredSets.Count == 0)
                    throw new DiagramParseException("Mermaid venn unions must appear after set declarations.");

                var union = ParseUnionDeclaration(line["union ".Length..]);
                var memberIndices = union.MemberIds
                    .Select(id =>
                    {
                        if (!setIndices.TryGetValue(id, out var index))
                            throw new DiagramParseException($"Mermaid venn union references unknown set '{id}'.");
                        return index;
                    })
                    .Distinct()
                    .OrderBy(index => index)
                    .ToArray();

                string region = MapRegion(memberIndices, declaredSets.Count);
                var overlapNode = GetOrCreateOverlapNode(region, diagram, overlapNodesByRegion);
                if (union.Label is not null)
                    overlapNode.Label.Text = union.Label;

                currentTextTarget = new IndentedTextTarget(VennTextTargetKind.Overlap, region, indent, CountAttachedTextNodes(diagram, region));
                continue;
            }

            if (line.StartsWith("style ", StringComparison.Ordinal))
            {
                var styleDeclaration = ParseStyleDeclaration(line["style ".Length..]);
                ApplyStyles(styleDeclaration.Targets, styleDeclaration.Styles, declaredSets.Count, setIndices, setNodesById, textNodesById, overlapNodesByRegion, diagram);
                continue;
            }

            if (line.StartsWith("text ", StringComparison.Ordinal))
            {
                throw new DiagramParseException("Mermaid venn text nodes must be indented under a set or union.");
            }

            throw new DiagramParseException($"Unsupported Mermaid venn statement: '{line}'.");
        }

        if (declaredSets.Count == 0)
            throw new DiagramParseException("Mermaid venn diagrams require at least one set declaration.");

        if (declaredSets.Count > 3)
            throw new DiagramParseException("Mermaid venn support currently handles up to 3 sets.");

        return diagram;
    }

    private static (string Id, string Label) ParseEntityDeclaration(string text)
    {
        string content = StripTrailingSize(text.Trim());
        int labelStart = FindTrailingBracketStart(content);
        string idPart = labelStart >= 0 ? content[..labelStart].Trim() : content;
        if (ContainsTopLevelComma(idPart))
            throw new DiagramParseException("Mermaid venn set declarations require exactly one identifier.");

        var (id, label) = ParseReferenceWithOptionalLabel(content);
        return (id, label ?? id);
    }

    private static (string[] MemberIds, string? Label) ParseUnionDeclaration(string text)
    {
        string content = StripTrailingSize(text.Trim());
        string? label = null;

        int labelStart = FindTrailingBracketStart(content);
        if (labelStart >= 0)
        {
            label = Unquote(content[(labelStart + 1)..^1].Trim());
            content = content[..labelStart].TrimEnd();
        }

        var members = SplitCommaSeparated(content)
            .Select(Unquote)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();

        if (members.Length < 2)
            throw new DiagramParseException("Mermaid venn union declarations require at least two set identifiers.");

        return (members, label);
    }

    private static (string Id, string? Label) ParseTextDeclaration(string text)
    {
        string content = text.Trim();
        if (string.IsNullOrEmpty(content))
            throw new DiagramParseException("Mermaid venn text declarations require an identifier or quoted text.");

        var (id, label) = ParseReferenceWithOptionalLabel(content);
        return (id, label);
    }

    private static (string[] Targets, Dictionary<string, string> Styles) ParseStyleDeclaration(string text)
    {
        text = text.Trim();
        int separatorIndex = FindWhitespaceOutsideQuotes(text);
        if (separatorIndex < 0)
            throw new DiagramParseException("Mermaid venn style statements require targets followed by style properties.");

        string targetsPart = text[..separatorIndex].Trim();
        string stylesPart = text[separatorIndex..].Trim();
        if (stylesPart.Length == 0)
            throw new DiagramParseException("Mermaid venn style statements require at least one style property.");

        var targets = ParseIdentifierList(targetsPart);
        if (targets.Length == 0)
            throw new DiagramParseException("Mermaid venn style statements require at least one target.");

        var styles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in SplitStyleFields(stylesPart))
        {
            int colonIndex = field.IndexOf(':');
            if (colonIndex <= 0)
                throw new DiagramParseException($"Invalid Mermaid venn style property '{field}'.");

            string key = field[..colonIndex].Trim();
            string value = field[(colonIndex + 1)..].Trim();
            if (value.Length == 0)
                throw new DiagramParseException($"Invalid Mermaid venn style property '{field}'.");

            if (!IsSupportedStyleProperty(key))
                throw new DiagramParseException($"Unsupported Mermaid venn style property '{key}'.");

            styles[key] = Unquote(value);
        }

        return (targets, styles);
    }

    private static void ApplyStyles(
        string[] targets,
        Dictionary<string, string> styles,
        int declaredSetCount,
        Dictionary<string, int> setIndices,
        Dictionary<string, Node> setNodesById,
        Dictionary<string, Node> textNodesById,
        Dictionary<string, Node> overlapNodesByRegion,
        Diagram diagram)
    {
        Node targetNode;

        if (targets.Length > 1)
        {
            var memberIndices = targets
                .Select(id =>
                {
                    if (!setIndices.TryGetValue(id, out var index))
                        throw new DiagramParseException($"Mermaid venn style references unknown set '{id}'.");

                    return index;
                })
                .Distinct()
                .OrderBy(index => index)
                .ToArray();

            string region = MapRegion(memberIndices, declaredSetCount);
            targetNode = GetOrCreateOverlapNode(region, diagram, overlapNodesByRegion);
        }
        else if (textNodesById.TryGetValue(targets[0], out targetNode!)
            || setNodesById.TryGetValue(targets[0], out targetNode!))
        {
        }
        else
        {
            throw new DiagramParseException($"Mermaid venn style references unknown target '{targets[0]}'.");
        }

        foreach (var (key, value) in styles)
        {
            switch (key.ToLowerInvariant())
            {
                case "fill":
                    targetNode.FillColor = value;
                    break;
                case "stroke":
                    targetNode.StrokeColor = value;
                    break;
                case "color":
                    targetNode.Label.Color = value;
                    break;
                case "fill-opacity":
                    if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fillOpacity))
                        throw new DiagramParseException($"Invalid Mermaid venn fill-opacity '{value}'.");

                    targetNode.Metadata[FillOpacityMetadataKey] = fillOpacity;
                    break;
            }
        }
    }

    private static string MapRegion(int[] indices, int declaredSetCount)
    {
        if (declaredSetCount == 2)
        {
            return indices.SequenceEqual([0, 1])
                ? "ab"
                : throw new DiagramParseException("Two-set Mermaid venn diagrams only support the union of both sets.");
        }

        if (declaredSetCount != 3)
            throw new DiagramParseException("Mermaid venn overlap labels currently require 2 or 3 sets.");

        return indices.Length switch
        {
            2 when indices.SequenceEqual([0, 1]) => "ab",
            2 when indices.SequenceEqual([0, 2]) => "ac",
            2 when indices.SequenceEqual([1, 2]) => "bc",
            3 when indices.SequenceEqual([0, 1, 2]) => "abc",
            _ => throw new DiagramParseException("Unsupported Mermaid venn union region for the current set order."),
        };
    }

    private static (string Id, string? Label) ParseReferenceWithOptionalLabel(string text)
    {
        int labelStart = FindTrailingBracketStart(text);
        if (labelStart < 0)
            return (Unquote(text.Trim()), null);

        string id = Unquote(text[..labelStart].Trim());
        string label = Unquote(text[(labelStart + 1)..^1].Trim());
        return (id, label);
    }

    private static int FindTrailingBracketStart(string text)
    {
        text = text.Trim();
        if (!text.EndsWith(']'))
            return -1;

        bool inQuotes = false;
        for (int i = text.Length - 1; i >= 0; i--)
        {
            char ch = text[i];
            if (ch == '"')
                inQuotes = !inQuotes;
            else if (ch == '[' && !inQuotes)
                return i;
        }

        return -1;
    }

    private static string StripTrailingSize(string text)
    {
        bool inQuotes = false;
        bool inBrackets = false;
        for (int i = text.Length - 1; i >= 0; i--)
        {
            char ch = text[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes)
            {
                if (ch == ']')
                {
                    inBrackets = true;
                    continue;
                }

                if (ch == '[')
                {
                    inBrackets = false;
                    continue;
                }

                if (ch == ':' && !inBrackets && IsNumeric(text[(i + 1)..].Trim()))
                    return text[..i].TrimEnd();
            }
        }

        return text;
    }

    private static bool IsNumeric(string text) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);

    private static IEnumerable<string> SplitCommaSeparated(string text)
    {
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (char ch in text)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                current.Append(ch);
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                yield return current.ToString().Trim();
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            yield return current.ToString().Trim();
    }

    private static IEnumerable<string> SplitStyleFields(string text)
    {
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        int parenthesisDepth = 0;

        foreach (char ch in text)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                current.Append(ch);
                continue;
            }

            if (!inQuotes)
            {
                if (ch == '(')
                {
                    parenthesisDepth++;
                }
                else if (ch == ')' && parenthesisDepth > 0)
                {
                    parenthesisDepth--;
                }
                else if (ch == ',' && parenthesisDepth == 0)
                {
                    if (current.Length > 0)
                        yield return current.ToString().Trim();

                    current.Clear();
                    continue;
                }
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            yield return current.ToString().Trim();
    }

    private static string[] ParseIdentifierList(string text) =>
        SplitCommaSeparated(text)
            .Select(Unquote)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();

    private static bool ContainsTopLevelComma(string text)
    {
        bool inQuotes = false;
        foreach (char ch in text)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
                return true;
        }

        return false;
    }

    private static int CountIndent(string text)
    {
        int count = 0;
        while (count < text.Length && (text[count] == ' ' || text[count] == '\t'))
            count++;

        return count;
    }

    private static int FindWhitespaceOutsideQuotes(string text)
    {
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
                return i;
        }

        return -1;
    }

    private static bool IsSupportedStyleProperty(string propertyName) =>
        propertyName.Equals("fill", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("stroke", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("color", StringComparison.OrdinalIgnoreCase)
        || propertyName.Equals("fill-opacity", StringComparison.OrdinalIgnoreCase);

    private static int CountAttachedTextNodes(Diagram diagram, string region) =>
        diagram.Nodes.Values.Count(node =>
            string.Equals(node.Metadata.GetValueOrDefault(VennKindMetadataKey) as string, "text", StringComparison.Ordinal)
            && string.Equals(node.Metadata.GetValueOrDefault(VennRegionMetadataKey) as string, region, StringComparison.Ordinal));

    private static Node GetOrCreateOverlapNode(string region, Diagram diagram, Dictionary<string, Node> overlapNodesByRegion)
    {
        if (overlapNodesByRegion.TryGetValue(region, out var existing))
            return existing;

        var overlapNode = new Node($"overlap_{region}", string.Empty);
        overlapNode.Metadata[VennKindMetadataKey] = "overlap";
        overlapNode.Metadata[VennRegionMetadataKey] = region;
        overlapNode.Metadata[RenderTextOnlyMetadataKey] = true;
        diagram.AddNode(overlapNode);
        overlapNodesByRegion[region] = overlapNode;
        return overlapNode;
    }

    private static string Unquote(string text)
    {
        text = text.Trim();
        return text.Length >= 2 && text[0] == '"' && text[^1] == '"'
            ? text[1..^1]
            : text;
    }

    private enum VennTextTargetKind
    {
        Set,
        Overlap,
    }

    private readonly record struct IndentedTextTarget(VennTextTargetKind Kind, string Key, int Indent, int NextTextIndex);
}