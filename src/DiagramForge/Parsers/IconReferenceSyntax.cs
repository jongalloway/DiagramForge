namespace DiagramForge.Parsers;

internal static class IconReferenceSyntax
{
    internal static (string Label, string? IconRef) Extract(string rawText)
    {
        var text = rawText.Trim();
        if (text.Length == 0)
            return (string.Empty, null);

        if (TryExtractLeading(text, out var label, out var iconRef))
            return (label, iconRef);

        if (TryExtractTrailing(text, out label, out iconRef))
            return (label, iconRef);

        return (text, null);
    }

    private static bool TryExtractLeading(string text, out string label, out string? iconRef)
    {
        label = text;
        iconRef = null;

        if (!text.StartsWith("icon:", StringComparison.OrdinalIgnoreCase))
            return false;

        int whitespaceIndex = text.IndexOfAny([' ', '\t', '\r', '\n']);
        string token = whitespaceIndex >= 0 ? text[..whitespaceIndex] : text;
        if (!TryNormalizeToken(token, out iconRef))
            return false;

        label = whitespaceIndex >= 0 ? text[whitespaceIndex..].Trim() : string.Empty;
        return true;
    }

    private static bool TryExtractTrailing(string text, out string label, out string? iconRef)
    {
        label = text;
        iconRef = null;

        if (!text.EndsWith(']'))
            return false;

        int tokenStart = text.LastIndexOf("[icon:", StringComparison.OrdinalIgnoreCase);
        if (tokenStart < 0)
            return false;

        string token = text[(tokenStart + 1)..^1].Trim();
        if (!TryNormalizeToken(token, out iconRef))
            return false;

        label = text[..tokenStart].TrimEnd();
        return true;
    }

    private static bool TryNormalizeToken(string token, out string? iconRef)
    {
        iconRef = null;

        if (!token.StartsWith("icon:", StringComparison.OrdinalIgnoreCase))
            return false;

        string value = token["icon:".Length..].Trim();
        int separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
            return false;

        iconRef = value;
        return true;
    }
}