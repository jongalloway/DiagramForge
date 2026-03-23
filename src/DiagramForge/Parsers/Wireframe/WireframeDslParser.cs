using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Parsers.Wireframe;

/// <summary>
/// Parses a subset of the <a href="https://github.com/MegaByteMark/markdown-ui-dsl">markdown-ui-dsl</a>
/// wireframe syntax into the unified <see cref="Diagram"/> model.
/// </summary>
/// <remarks>
/// <para>The wireframe DSL renders UI mockups as SVG. Example:</para>
/// <code>
/// wireframe: Login Screen
/// ::: HEADER :::
///   # Login
/// --- END ---
/// [ text: Username ]
/// [ text: Password ]
/// [ Login ](#login)
/// </code>
/// <para>
/// The first non-empty line must start with <c>wireframe</c> (optionally followed by <c>: title</c>).
/// Supported elements: containers (COLUMN, ROW, CARD, HEADER, FOOTER), buttons, text inputs,
/// checkboxes, radio buttons, toggles, dropdowns, tab bars, badges, image placeholders,
/// headings, and plain text.
/// </para>
/// </remarks>
public sealed class WireframeDslParser : IDiagramParser
{
    /// <summary>The synthetic root container ID used internally by the layout engine.</summary>
    internal const string RootNodeId = "wf_root";

    public string SyntaxId => "wireframe";

    /// <inheritdoc/>
    public bool CanParse(string diagramText)
    {
        if (string.IsNullOrWhiteSpace(diagramText))
            return false;

        foreach (var line in diagramText.AsSpan().EnumerateLines())
        {
            var trimmed = line.Trim();
            if (trimmed.IsEmpty)
                continue;
            return trimmed.StartsWith("wireframe", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <inheritdoc/>
    public Diagram Parse(string diagramText)
    {
        if (string.IsNullOrWhiteSpace(diagramText))
            throw new DiagramParseException("Diagram text cannot be null or empty.");

        var rawLines = diagramText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n');

        if (rawLines.Length == 0)
            throw new DiagramParseException("Diagram text is empty.");

        // Locate the header line ("wireframe [: title]") to find where the body starts.
        int startLine = 0;

        for (int i = 0; i < rawLines.Length; i++)
        {
            var trimmed = rawLines[i].Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (!trimmed.StartsWith("wireframe", StringComparison.OrdinalIgnoreCase))
                throw new DiagramParseException(
                    "Wireframe DSL must begin with 'wireframe' or 'wireframe: <title>'.");

            startLine = i + 1;
            break;
        }

        var builder = new DiagramSemanticModelBuilder()
            .WithSourceSyntax(SyntaxId)
            .WithDiagramType("wireframe");

        // The wireframe declaration line ("wireframe: Title") identifies the diagram
        // but does not render a visible title. Use ::: HEADER ::: for visual headers.

        ParseBody(rawLines, startLine, builder);

        return builder.Build();
    }

    // ── Body parsing ──────────────────────────────────────────────────────────

    private static void ParseBody(string[] lines, int startLine, IDiagramSemanticModelBuilder builder)
    {
        // Synthetic invisible root column that holds all top-level items.
        var rootNode = new Node(RootNodeId, string.Empty);
        rootNode.Metadata["wireframe:kind"] = "column";
        rootNode.Metadata["wireframe:isRoot"] = true;
        builder.AddNode(rootNode);

        var containerStack = new Stack<string>();
        containerStack.Push(RootNodeId);

        int nodeCounter = 0;

        for (int i = startLine; i < lines.Length; i++)
        {
            var raw = lines[i];
            var trimmed = raw.Trim();

            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Container start
            if (TryParseContainerStart(trimmed, out var containerKind, out var containerTitle))
            {
                var nodeId = $"wf_{nodeCounter++}";
                var node = new Node(nodeId, containerTitle ?? string.Empty);
                node.Metadata["wireframe:kind"] = containerKind;
                builder.AddNode(node);

                if (containerStack.TryPeek(out var parentId))
                    AddContainmentEdge(builder, parentId, nodeId);

                containerStack.Push(nodeId);
                continue;
            }

            // Container end — pop, but never pop the root.
            if (IsContainerEnd(trimmed))
            {
                if (containerStack.Count > 1)
                    containerStack.Pop();
                continue;
            }

            // Inline badge+text: "(( Label )) trailing text" → implicit row with badge + text.
            if (TrySplitInlineBadge(trimmed, out var badgeLabel, out var trailingText))
            {
                // Wrap in an implicit row so badge and text render side-by-side.
                var rowId = $"wf_{nodeCounter++}";
                var rowNode = new Node(rowId, string.Empty);
                rowNode.Metadata["wireframe:kind"] = "row";
                builder.AddNode(rowNode);
                if (containerStack.TryPeek(out var rp))
                    AddContainmentEdge(builder, rp, rowId);

                var badgeNode = new Node($"wf_{nodeCounter++}", badgeLabel);
                badgeNode.Metadata["wireframe:kind"] = "badge";
                builder.AddNode(badgeNode);
                AddContainmentEdge(builder, rowId, badgeNode.Id);

                if (!string.IsNullOrWhiteSpace(trailingText))
                {
                    var textNode = new Node($"wf_{nodeCounter++}", trailingText);
                    textNode.Metadata["wireframe:kind"] = "text";
                    builder.AddNode(textNode);
                    AddContainmentEdge(builder, rowId, textNode.Id);
                }
                continue;
            }

            // Leaf component
            var leaf = ParseLeafComponent(trimmed, $"wf_{nodeCounter++}");
            if (leaf is not null)
            {
                builder.AddNode(leaf);
                if (containerStack.TryPeek(out var parentId))
                    AddContainmentEdge(builder, parentId, leaf.Id);
            }
        }

        builder.WithLayoutHints(new LayoutHints
        {
            Direction = LayoutDirection.TopToBottom,
            MinNodeWidth = 280,
        });
    }

    private static void AddContainmentEdge(IDiagramSemanticModelBuilder builder, string parentId, string childId)
    {
        var edge = new Edge(parentId, childId)
        {
            ArrowHead = ArrowHeadStyle.None,
            Routing = EdgeRouting.Straight,
        };
        edge.Metadata["wireframe:containment"] = true;
        builder.AddEdge(edge);
    }

    // ── Container detection ───────────────────────────────────────────────────

    private static bool TryParseContainerStart(string line, out string kind, out string? title)
    {
        kind = string.Empty;
        title = null;

        // ||| COLUMN ||| or ||| COLUMN: Title |||
        if (line.StartsWith("|||", StringComparison.Ordinal)
            && line.EndsWith("|||", StringComparison.Ordinal)
            && line.Length > 6)
        {
            var inner = line[3..^3].Trim();
            if (inner.StartsWith("COLUMN", StringComparison.OrdinalIgnoreCase))
            {
                kind = "column";
                title = ExtractContainerTitle(inner, "COLUMN");
                return true;
            }
        }

        // === ROW ===
        if (line.StartsWith("===", StringComparison.Ordinal)
            && line.EndsWith("===", StringComparison.Ordinal)
            && line.Length > 6)
        {
            var inner = line[3..^3].Trim();
            if (inner.StartsWith("ROW", StringComparison.OrdinalIgnoreCase))
            {
                kind = "row";
                title = ExtractContainerTitle(inner, "ROW");
                return true;
            }
        }

        // ::: CARD ::: / ::: HEADER ::: / ::: FOOTER :::
        if (line.StartsWith(":::", StringComparison.Ordinal)
            && line.EndsWith(":::", StringComparison.Ordinal)
            && line.Length > 6)
        {
            var inner = line[3..^3].Trim();
            if (inner.StartsWith("HEADER", StringComparison.OrdinalIgnoreCase))
            {
                kind = "header";
                title = ExtractContainerTitle(inner, "HEADER");
                return true;
            }
            if (inner.StartsWith("FOOTER", StringComparison.OrdinalIgnoreCase))
            {
                kind = "footer";
                title = ExtractContainerTitle(inner, "FOOTER");
                return true;
            }
            if (inner.StartsWith("CARD", StringComparison.OrdinalIgnoreCase))
            {
                kind = "card";
                title = ExtractContainerTitle(inner, "CARD");
                return true;
            }
        }

        return false;
    }

    private static string? ExtractContainerTitle(string inner, string keyword)
    {
        int colonPos = inner.IndexOf(':', keyword.Length);
        if (colonPos < 0)
            return null;
        var t = inner[(colonPos + 1)..].Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static bool IsContainerEnd(string line)
        => line.StartsWith("---", StringComparison.Ordinal)
           && line.EndsWith("---", StringComparison.Ordinal)
           && line.Contains("END", StringComparison.OrdinalIgnoreCase);

    // ── Leaf component parsing ────────────────────────────────────────────────

    private static Node? ParseLeafComponent(string line, string nodeId)
    {
        // *** divider
        if (line.StartsWith("***", StringComparison.Ordinal))
        {
            var n = new Node(nodeId, string.Empty);
            n.Metadata["wireframe:kind"] = "divider";
            return n;
        }

        // Bracket-based components
        if (line.StartsWith("[", StringComparison.Ordinal))
            return ParseBracketComponent(line, nodeId);

        // Paren-based components
        if (line.StartsWith("(", StringComparison.Ordinal))
            return ParseParenComponent(line, nodeId);

        // Tab bar: |[ Active ]| Tab2 | Tab3 |
        if (line.StartsWith("|", StringComparison.Ordinal))
            return ParseTabBar(line, nodeId);

        // Headings
        if (line.StartsWith("#", StringComparison.Ordinal))
            return ParseHeading(line, nodeId);

        // > layout hints — skip (static SVG output only)
        if (line.StartsWith(">", StringComparison.Ordinal))
            return null;

        // Bold **text** and plain text / list items
        var text = line.TrimStart('-').Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        // Strip **bold** markers for label text (bold styling comes from wireframe:bold metadata).
        // Minimum: "**x**" = 5 chars: 2 open markers + 1 body char + 2 close markers.
        const int BoldWrapperLength = 4; // "**" prefix + "**" suffix
        bool isBold = text.StartsWith("**", StringComparison.Ordinal)
                      && text.EndsWith("**", StringComparison.Ordinal)
                      && text.Length > BoldWrapperLength;
        if (isBold)
            text = text[2..^2].Trim();

        var textNode = new Node(nodeId, text);
        textNode.Metadata["wireframe:kind"] = "text";
        if (isBold)
            textNode.Metadata["wireframe:bold"] = true;
        return textNode;
    }

    private static Node? ParseBracketComponent(string line, string nodeId)
    {
        // [on] / [off] → Toggle (case-insensitive, optional trailing label)
        const string OnToken = "[on]";
        const string OffToken = "[off]";

        if (string.Equals(line, OnToken, StringComparison.OrdinalIgnoreCase)
            || (line.Length > OnToken.Length
                && string.Equals(line[..OnToken.Length], OnToken, StringComparison.OrdinalIgnoreCase)
                && line[OnToken.Length] == ' '))
        {
            var label = line.Length > OnToken.Length + 1 ? line[(OnToken.Length + 1)..].Trim() : string.Empty;
            var n = new Node(nodeId, label);
            n.Metadata["wireframe:kind"] = "toggle";
            n.Metadata["wireframe:on"] = true;
            return n;
        }
        if (string.Equals(line, OffToken, StringComparison.OrdinalIgnoreCase)
            || (line.Length > OffToken.Length
                && string.Equals(line[..OffToken.Length], OffToken, StringComparison.OrdinalIgnoreCase)
                && line[OffToken.Length] == ' '))
        {
            var label = line.Length > OffToken.Length + 1 ? line[(OffToken.Length + 1)..].Trim() : string.Empty;
            var n = new Node(nodeId, label);
            n.Metadata["wireframe:kind"] = "toggle";
            n.Metadata["wireframe:on"] = false;
            return n;
        }

        // [ ] Label → Checkbox unchecked
        if (line.StartsWith("[ ] ", StringComparison.Ordinal) || line.Equals("[ ]", StringComparison.Ordinal))
        {
            var label = line.Length > 4 ? line[4..].Trim() : string.Empty;
            var n = new Node(nodeId, label);
            n.Metadata["wireframe:kind"] = "checkbox";
            n.Metadata["wireframe:checked"] = false;
            return n;
        }

        // [x] Label → Checkbox checked (case-insensitive x)
        if (line.Length >= 3
            && line[0] == '['
            && char.ToUpperInvariant(line[1]) == 'X'
            && line[2] == ']'
            && (line.Length == 3 || line[3] == ' '))
        {
            var label = line.Length > 4 ? line[4..].Trim() : string.Empty;
            var n = new Node(nodeId, label);
            n.Metadata["wireframe:kind"] = "checkbox";
            n.Metadata["wireframe:checked"] = true;
            return n;
        }

        // [v] Value {Opt1, Opt2} → Dropdown
        if (line.Length >= 3
            && line[0] == '['
            && char.ToUpperInvariant(line[1]) == 'V'
            && line[2] == ']')
        {
            var rest = line[3..].Trim();
            var value = rest;
            string? options = null;
            int braceOpen = rest.IndexOf('{', StringComparison.Ordinal);
            int braceClose = rest.LastIndexOf('}');
            if (braceOpen >= 0 && braceClose > braceOpen)
            {
                value = rest[..braceOpen].Trim();
                options = rest[(braceOpen + 1)..braceClose].Trim();
            }
            var n = new Node(nodeId, value);
            n.Metadata["wireframe:kind"] = "dropdown";
            if (options is not null)
                n.Metadata["wireframe:options"] = options;
            return n;
        }

        // Locate closing bracket
        int closeBracket = line.IndexOf(']', StringComparison.Ordinal);
        if (closeBracket < 0)
        {
            var fallback = new Node(nodeId, line);
            fallback.Metadata["wireframe:kind"] = "text";
            return fallback;
        }

        var bracketContent = line[1..closeBracket].Trim();

        // [ IMG: Description ] → Image placeholder
        if (bracketContent.StartsWith("IMG:", StringComparison.OrdinalIgnoreCase))
        {
            var desc = bracketContent[4..].Trim();
            var n = new Node(nodeId, desc);
            n.Metadata["wireframe:kind"] = "image";
            return n;
        }

        // [ text: Placeholder ] → Text input
        if (bracketContent.StartsWith("text:", StringComparison.OrdinalIgnoreCase))
        {
            var placeholder = bracketContent[5..].Trim();
            var n = new Node(nodeId, placeholder);
            n.Metadata["wireframe:kind"] = "textinput";
            return n;
        }

        // [ Button Name ] or [ Button Name ](#action) → Button
        var buttonNode = new Node(nodeId, bracketContent);
        buttonNode.Metadata["wireframe:kind"] = "button";

        var afterBracket = line[(closeBracket + 1)..].Trim();
        if (afterBracket.StartsWith("(", StringComparison.Ordinal))
        {
            int closeP = afterBracket.IndexOf(')', StringComparison.Ordinal);
            if (closeP > 0)
            {
                var action = afterBracket[1..closeP].TrimStart('#').Trim();
                if (!string.IsNullOrEmpty(action))
                    buttonNode.Metadata["wireframe:action"] = action;
            }
        }

        return buttonNode;
    }

    private static Node? ParseParenComponent(string line, string nodeId)
    {
        // (( Label )) → Badge
        if (line.StartsWith("((", StringComparison.Ordinal) && line.EndsWith("))", StringComparison.Ordinal) && line.Length > 4)
        {
            var label = line[2..^2].Trim();
            var n = new Node(nodeId, label);
            n.Metadata["wireframe:kind"] = "badge";
            return n;
        }

        // ( ) Label → Radio unchecked
        if (line.StartsWith("( ) ", StringComparison.Ordinal) || line.Equals("( )", StringComparison.Ordinal))
        {
            var label = line.Length > 4 ? line[4..].Trim() : string.Empty;
            var n = new Node(nodeId, label);
            n.Metadata["wireframe:kind"] = "radio";
            n.Metadata["wireframe:checked"] = false;
            return n;
        }

        // (x) Label → Radio checked
        if (line.Length >= 3
            && line[0] == '('
            && char.ToUpperInvariant(line[1]) == 'X'
            && line[2] == ')'
            && (line.Length == 3 || line[3] == ' '))
        {
            var label = line.Length > 4 ? line[4..].Trim() : string.Empty;
            var n = new Node(nodeId, label);
            n.Metadata["wireframe:kind"] = "radio";
            n.Metadata["wireframe:checked"] = true;
            return n;
        }

        // Fallback to text
        var textNode = new Node(nodeId, line);
        textNode.Metadata["wireframe:kind"] = "text";
        return textNode;
    }

    /// <summary>
    /// Detects "(( Label )) trailing text" on a single line and splits it into
    /// a badge label and optional trailing text so the body parser can emit two nodes.
    /// </summary>
    private static bool TrySplitInlineBadge(string line, out string badgeLabel, out string trailingText)
    {
        badgeLabel = string.Empty;
        trailingText = string.Empty;

        if (!line.StartsWith("((", StringComparison.Ordinal))
            return false;

        int closeIdx = line.IndexOf("))", 2, StringComparison.Ordinal);
        if (closeIdx < 0)
            return false;

        // Only split when there is content after the closing "))"
        // (standalone badges are handled by ParseParenComponent).
        if (closeIdx + 2 >= line.Length)
            return false;

        badgeLabel = line[2..closeIdx].Trim();
        trailingText = line[(closeIdx + 2)..].Trim();
        // Require non-empty trailing text so a standalone badge with only trailing
        // whitespace (e.g. "(( Info )) ") is not treated as an inline badge row.
        return badgeLabel.Length > 0 && trailingText.Length > 0;
    }

    private static Node? ParseTabBar(string line, string nodeId)
    {
        // |[ Active ]| Tab2 | Tab3 |
        var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var tabNames = new List<string>();
        int activeTab = 0;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                activeTab = tabNames.Count;
                tabNames.Add(trimmed[1..^1].Trim());
            }
            else
            {
                tabNames.Add(trimmed);
            }
        }

        if (tabNames.Count == 0)
            return null;

        var n = new Node(nodeId, string.Join(" | ", tabNames));
        n.Metadata["wireframe:kind"] = "tabs";
        n.Metadata["wireframe:tabs"] = tabNames.ToArray();
        n.Metadata["wireframe:activeTab"] = activeTab;
        return n;
    }

    private static Node ParseHeading(string line, string nodeId)
    {
        int level = 0;
        while (level < line.Length && line[level] == '#')
            level++;
        level = Math.Clamp(level, 1, 3);
        var text = line[level..].Trim();
        var n = new Node(nodeId, text);
        n.Metadata["wireframe:kind"] = "heading";
        n.Metadata["wireframe:headingLevel"] = level;
        return n;
    }
}
