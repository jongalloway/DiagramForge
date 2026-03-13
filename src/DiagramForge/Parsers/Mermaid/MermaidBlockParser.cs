using System.Globalization;
using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Mermaid;

internal sealed class MermaidBlockParser : IMermaidDiagramParser
{
    private const string BlockColumnCountKey = "block:columnCount";

    // Metadata key prefix for composite block group layout info stored in diagram.Metadata.
    // Full key format: "block:group:{id}:row" / ":column" / ":span" / ":columnCount"
    internal const string GroupMetaPrefix = "block:group:";

    public bool CanParse(MermaidDiagramKind kind) => kind == MermaidDiagramKind.BlockDiagram;

    public Diagram Parse(MermaidDocument document)
    {
        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax("mermaid")
            .WithDiagramType("block");

        var diagram = builder.Build();
        var nodesSeen = new Dictionary<string, Node>(StringComparer.Ordinal);

        // --- mutable grid-state variables (represent the *current* block level) ---
        int configuredColumns = -1;
        int currentRow = 0;
        int currentColumn = 0;
        int maxColumn = 0;
        string? currentGroupId = null;   // null ⇒ top-level

        // Stack for composite block nesting.  Each frame saves the outer grid state
        // so we can restore it when the composite block ends.
        var compositeBlockStack = new Stack<(
            string GroupId,
            int Span,
            int SavedConfiguredColumns,
            int SavedCurrentRow,
            int SavedCurrentColumn,
            int SavedMaxColumn,
            string? SavedGroupId)>();

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
                if (value.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    configuredColumns = -1;
                }
                else if (!int.TryParse(value, CultureInfo.InvariantCulture, out configuredColumns) || configuredColumns < 1)
                {
                    throw new DiagramParseException($"Invalid column count '{value}': must be a positive integer or 'auto'.");
                }
                AdvanceToNextRow();
                continue;
            }

            // ── composite block opening: block:<id> or block:<id>:<span> ─────────
            if (TryParseCompositeBlockOpening(line, out var cbId, out var cbSpan))
            {
                // Check for overflow into the next outer row.
                if (configuredColumns > 0 && currentColumn + cbSpan > configuredColumns)
                {
                    currentRow++;
                    currentColumn = 0;
                }

                // Record the position of this composite block in the outer grid.
                diagram.Metadata[$"{GroupMetaPrefix}{cbId}:row"] = currentRow;
                diagram.Metadata[$"{GroupMetaPrefix}{cbId}:column"] = currentColumn;
                diagram.Metadata[$"{GroupMetaPrefix}{cbId}:span"] = cbSpan;

                // Reserve the span in the current (outer) grid *before* pushing.
                ReserveColumns(cbSpan);

                // Save the current outer grid state and push a new inner context.
                compositeBlockStack.Push((
                    GroupId: cbId,
                    Span: cbSpan,
                    SavedConfiguredColumns: configuredColumns,
                    SavedCurrentRow: currentRow,
                    SavedCurrentColumn: currentColumn,
                    SavedMaxColumn: maxColumn,
                    SavedGroupId: currentGroupId));

                // Reset inner grid state.
                configuredColumns = -1;
                currentRow = 0;
                currentColumn = 0;
                maxColumn = 0;
                currentGroupId = cbId;

                // Create the group (or retrieve it if the id was seen before).
                if (!diagram.Groups.Exists(g => g.Id == cbId))
                    diagram.AddGroup(new Group(cbId));

                // Do NOT call AdvanceToNextRow here – the composite block opener does
                // not itself represent a row of content in the outer grid.
                continue;
            }

            // ── composite block closing ───────────────────────────────────────────
            if (line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                if (compositeBlockStack.Count > 0)
                {
                    // Store inner column count before popping.
                    var innerColumnCount = configuredColumns > 0 ? configuredColumns : Math.Max(1, maxColumn);
                    diagram.Metadata[$"{GroupMetaPrefix}{currentGroupId}:columnCount"] = innerColumnCount;

                    var frame = compositeBlockStack.Pop();
                    configuredColumns = frame.SavedConfiguredColumns;
                    currentRow = frame.SavedCurrentRow;
                    currentColumn = frame.SavedCurrentColumn;
                    maxColumn = frame.SavedMaxColumn;
                    currentGroupId = frame.SavedGroupId;
                }
                // Whether or not we popped a frame, skip the line.
                continue;
            }

            if (line.StartsWith("style ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("class ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("classDef ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("linkStyle ", StringComparison.OrdinalIgnoreCase))
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

                    if (currentGroupId is not null)
                    {
                        node.Metadata["block:groupId"] = currentGroupId;
                        // Associate node with the group.
                        var grp = diagram.Groups.Find(g => g.Id == currentGroupId);
                        grp?.ChildNodeIds.Add(node.Id);
                    }

                    if (descriptor.ArrowDirection is not null)
                        node.Metadata["block:arrowDirection"] = descriptor.ArrowDirection;
                }

                ReserveColumns(descriptor.Span);
            }

            AdvanceToNextRow();
        }

        // If the document ended without closing all composite blocks, finalize them.
        while (compositeBlockStack.Count > 0)
        {
            var innerColumnCount = configuredColumns > 0 ? configuredColumns : Math.Max(1, maxColumn);
            diagram.Metadata[$"{GroupMetaPrefix}{currentGroupId}:columnCount"] = innerColumnCount;

            var frame = compositeBlockStack.Pop();
            configuredColumns = frame.SavedConfiguredColumns;
            currentRow = frame.SavedCurrentRow;
            currentColumn = frame.SavedCurrentColumn;
            maxColumn = frame.SavedMaxColumn;
            currentGroupId = frame.SavedGroupId;
        }

        diagram.Metadata[BlockColumnCountKey] = configuredColumns > 0 ? configuredColumns : Math.Max(1, maxColumn);
        return diagram;
    }

    // Detects "block:<id>" or "block:<id>:<span>" lines (composite block opening).
    // Returns false for any other input (including "block-beta" header lines, which
    // never appear in the inner-content loop since they're line index 0).
    private static bool TryParseCompositeBlockOpening(string line, out string id, out int span)
    {
        id = string.Empty;
        span = 1;

        if (!line.StartsWith("block:", StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = line[6..]; // everything after "block:"

        // Validate: must not be empty and must not contain whitespace (which would
        // indicate we mis-matched a multi-token line).
        if (rest.Length == 0 || rest.Any(char.IsWhiteSpace))
            return false;

        // Check for optional trailing span: "block:<id>:<n>"
        int lastColon = rest.LastIndexOf(':');
        if (lastColon > 0)
        {
            var possibleSpan = rest[(lastColon + 1)..];
            if (int.TryParse(possibleSpan, CultureInfo.InvariantCulture, out int parsedSpan) && parsedSpan >= 1)
            {
                id = rest[..lastColon];
                span = parsedSpan;
                return true;
            }
        }

        // No numeric suffix – the whole rest is the id.
        id = rest;
        span = 1;
        return true;
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
            var spaceSpanStr = token[6..];
            if (!int.TryParse(spaceSpanStr, CultureInfo.InvariantCulture, out int spaceSpan) || spaceSpan < 1)
                throw new DiagramParseException($"Invalid space span '{spaceSpanStr}': must be a positive integer.");
            return new BlockToken("", "", Shape.Rectangle, spaceSpan, IsSpace: true, ArrowDirection: null);
        }

        int span = 1;
        int spanSeparator = FindTrailingSpanSeparator(token);
        if (spanSeparator >= 0)
        {
            var spanStr = token[(spanSeparator + 1)..];
            if (!int.TryParse(spanStr, CultureInfo.InvariantCulture, out span) || span < 1)
                throw new DiagramParseException($"Invalid span value '{spanStr}': must be a positive integer.");
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