using DiagramForge.Icons;

namespace DiagramForge.Tests.Icons;

public class SvgIconSanitizerTests
{
    // ── Valid content passes through ──────────────────────────────────────────

    [Fact]
    public void Sanitize_ValidPath_PassesThrough()
    {
        const string input = """<path d="M0 0L10 10" fill="none" stroke="currentColor"/>""";
        var result = SvgIconSanitizer.Sanitize(input);

        Assert.NotNull(result);
        Assert.Contains("d=", result);
    }

    [Fact]
    public void Sanitize_ValidGroup_PassesThrough()
    {
        const string input = """<g fill="none"><circle cx="12" cy="12" r="10"/></g>""";
        var result = SvgIconSanitizer.Sanitize(input);

        Assert.NotNull(result);
        Assert.Contains("circle", result);
    }

    // ── Unsafe content is stripped ────────────────────────────────────────────

    [Fact]
    public void Sanitize_ScriptElement_IsRemoved()
    {
        const string input = """<g><script>alert('xss')</script><path d="M0 0"/></g>""";
        var result = SvgIconSanitizer.Sanitize(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path", result);
    }

    [Fact]
    public void Sanitize_StyleElement_IsRemoved()
    {
        const string input = """<g><style>.cls{fill:red}</style><rect x="0" y="0" width="10" height="10"/></g>""";
        var result = SvgIconSanitizer.Sanitize(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("style", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rect", result);
    }

    [Fact]
    public void Sanitize_ForeignObjectElement_IsRemoved()
    {
        const string input = """<g><foreignObject><div>html</div></foreignObject><path d="M0 0"/></g>""";
        var result = SvgIconSanitizer.Sanitize(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("foreignObject", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_OnClickAttribute_IsRemoved()
    {
        const string input = """<path d="M0 0" onclick="alert(1)"/>""";
        var result = SvgIconSanitizer.Sanitize(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("d=", result);
    }

    [Fact]
    public void Sanitize_OnLoadAttribute_IsRemoved()
    {
        const string input = """<g onload="malicious()"><circle cx="5" cy="5" r="5"/></g>""";
        var result = SvgIconSanitizer.Sanitize(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("onload", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_JavascriptUri_IsRemoved()
    {
        const string input = """<path d="M0 0" fill="javascript:alert(1)"/>""";
        var result = SvgIconSanitizer.Sanitize(input);

        Assert.NotNull(result);
        Assert.DoesNotContain("javascript", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Sanitize_Null_ReturnsNull()
    {
        Assert.Null(SvgIconSanitizer.Sanitize(null));
    }

    [Fact]
    public void Sanitize_Empty_ReturnsNull()
    {
        Assert.Null(SvgIconSanitizer.Sanitize(""));
        Assert.Null(SvgIconSanitizer.Sanitize("  "));
    }

    [Fact]
    public void Sanitize_InvalidXml_ReturnsNull()
    {
        Assert.Null(SvgIconSanitizer.Sanitize("not xml at all"));
    }

    [Fact]
    public void Sanitize_PreservesTransformAttribute()
    {
        const string input = """<g transform="translate(10,10)"><path d="M0 0"/></g>""";
        var result = SvgIconSanitizer.Sanitize(input);

        Assert.NotNull(result);
        Assert.Contains("transform", result);
    }
}
