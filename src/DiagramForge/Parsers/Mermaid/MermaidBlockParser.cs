using System.Globalization;
using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidBlockParser : IMermaidDiagramParser
{
    private const string BlockColumnCountKey = "block:columnCount";

    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.BlockDiagram;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("block");

        var diagram = builder.Build();
        var nodesSeen = new Dictionary<string, Node>(StringComparer.Ordinal);
        int configuredColumns = -1;
        int currentRow = 0;
        int currentColumn = 0;
        int maxColumn = 0;

        Node GetOrCreateNode(string id, string label)
        {
            if (!nodesSeen.TryGetValue(id, out var node))
            {
                node = new Node(id, label);
                nodesSeen[id] = node;
                diagram.AddNode(node);
            }

            return node;
        }

        void AdvanceToNextRow()
        {
            if (currentColumn > 0)
            {
                currentRow++;
                currentColumn = 0;
            }
        }

        void ReserveColumns(int span)
        {
            maxColumn = Math.Max(maxColumn, currentColumn + span);
            currentColumn += span;
        }

        for (int i = 1; i < document.RawLines.Length; i++)
        {
            var line = document.RawLines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("columns ", StringComparison.OrdinalIgnoreCase))
            {
                var value = line[8..].Trim();
                configuredColumns = value.Equals("auto", StringComparison.OrdinalIgnoreCase)
                    ? -1
                    : int.Parse(value, CultureInfo.InvariantCulture);
                AdvanceToNextRow();
                continue;
            }

            if (line.StartsWith("style ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("class ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("classDef ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("linkStyle ", StringComparison.OrdinalIgnoreCase)
                || line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryParseEdge(line, out var edge))
            {
                diagram.AddEdge(edge);
                continue;
            }

            var tokens = TokenizeLine(line);
            foreach (var token in tokens)
            {
                var descriptor = ParseToken(token);

                if (configuredColumns > 0 && currentColumn + descriptor.Span > configuredColumns)
                {
                    currentRow++;
                    currentColumn = 0;
                }

                if (!descriptor.IsSpace)
                {
                    var node = GetOrCreateNode(descriptor.Id, descriptor.Label);
                    node.Shape = descriptor.Shape;
                    node.Metadata["block:row"] = currentRow;
                    node.Metadata["block:column"] = currentColumn;
                    node.Metadata["block:span"] = descriptor.Span;

                    if (descriptor.ArrowDirection is not null)
                        node.Metadata["block:arrowDirection"] = descriptor.ArrowDirection;
                }

                ReserveColumns(descriptor.Span);
            }

            AdvanceToNextRow();
        }

        diagram.Metadata[BlockColumnCountKey] = configuredColumns > 0 ? configuredColumns : Math.Max(1, maxColumn);
        return diagram;
    }

    private static bool TryParseEdge(string line, out Edge edge)
    {
        edge = null!;

        var match = System.Text.RegularExpressions.Regex.Match(
            line,
            "^(?<src>.+?)\\s*--(?:\\s*\"(?<label>[^\"]+)\"\\s*--)?>\\s*(?<dst>.+)$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (!match.Success)
            return false;

        var source = match.Groups["src"].Value.Trim();
        var target = match.Groups["dst"].Value.Trim();
        edge = new Edge(source, target);
        if (match.Groups["label"].Success)
            edge.Label = new Label(match.Groups["label"].Value);

        return true;
    }

    private static List<string> TokenizeLine(string line)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        int squareDepth = 0;
        int parenDepth = 0;
        int braceDepth = 0;

        foreach (char ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                current.Append(ch);
                continue;
            }

            if (!inQuotes)
            {
                switch (ch)
                {
                    case '[':
                        squareDepth++;
                        break;
                    case ']':
                        squareDepth = Math.Max(0, squareDepth - 1);
                        break;
                    case '(':
                        parenDepth++;
                        break;
                    case ')':
                        parenDepth = Math.Max(0, parenDepth - 1);
                        break;
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth = Math.Max(0, braceDepth - 1);
                        break;
                }
            }

            if (char.IsWhiteSpace(ch) && !inQuotes && squareDepth == 0 && parenDepth == 0 && braceDepth == 0)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }

    private static BlockToken ParseToken(string token)
    {
        token = token.Trim();
        if (token.Equals("space", StringComparison.OrdinalIgnoreCase))
            return new BlockToken("", "", Shape.Rectangle, 1, IsSpace: true, ArrowDirection: null);

        if (token.StartsWith("space:", StringComparison.OrdinalIgnoreCase))
        {
            int spaceSpan = int.Parse(token[6..], CultureInfo.InvariantCulture);
            return new BlockToken("", "", Shape.Rectangle, spaceSpan, IsSpace: true, ArrowDirection: null);
        }

        int span = 1;
        int spanSeparator = FindTrailingSpanSeparator(token);
        if (spanSeparator >= 0)
        {
            span = int.Parse(token[(spanSeparator + 1)..], CultureInfo.InvariantCulture);
            token = token[..spanSeparator];
        }

        if (TryParseArrowBlock(token, out var arrowToken))
            return arrowToken with { Span = span };

        var (id, label, shape) = MermaidNodeSyntax.ParseNodeDeclaration(token);
        return new BlockToken(id, label, shape ?? Shape.Rectangle, span, IsSpace: false, ArrowDirection: null);
    }

    private static bool TryParseArrowBlock(string token, out BlockToken descriptor)
    {
        descriptor = default;

        int arrowStart = token.IndexOf("<[", StringComparison.Ordinal);
        int dirStart = token.LastIndexOf("]>(", StringComparison.Ordinal);
        if (arrowStart < 1 || dirStart < 0 || !token.EndsWith(')'))
            return false;

        string id = token[..arrowStart].Trim();
        string label = token[(arrowStart + 2)..dirStart].Trim().Trim('"');
        string directionList = token[(dirStart + 3)..^1].Trim();
        string direction = directionList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "right";

        var shape = direction.ToLowerInvariant() switch
        {
            "left" => Shape.ArrowLeft,
            "up" => Shape.ArrowUp,
            "down" => Shape.ArrowDown,
            _ => Shape.ArrowRight,
        };

        descriptor = new BlockToken(id, string.IsNullOrEmpty(label) ? id : label, shape, 1, IsSpace: false, ArrowDirection: direction);
        return true;
    }

    private static int FindTrailingSpanSeparator(string token)
    {
        bool inQuotes = false;
        int squareDepth = 0;
        int parenDepth = 0;
        int braceDepth = 0;

        for (int i = token.Length - 1; i >= 0; i--)
        {
            char ch = token[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
                continue;

            switch (ch)
            {
                case ']':
                    squareDepth++;
                    break;
                case '[':
                    squareDepth--;
                    break;
                case ')':
                    parenDepth++;
                    break;
                case '(':
                    parenDepth--;
                    break;
                case '}':
                    braceDepth++;
                    break;
                case '{':
                    braceDepth--;
                    break;
                case ':':
                    if (squareDepth == 0 && parenDepth == 0 && braceDepth == 0)
                        return i;
                    break;
            }
        }

        return -1;
    }

    private readonly record struct BlockToken(
        string Id,
        string Label,
        Shape Shape,
        int Span,
        bool IsSpace,
        string? ArrowDirection);
}