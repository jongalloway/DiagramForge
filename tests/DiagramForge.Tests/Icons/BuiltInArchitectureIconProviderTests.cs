using DiagramForge.Icons;

namespace DiagramForge.Tests.Icons;

public class BuiltInArchitectureIconProviderTests
{
    private readonly BuiltInArchitectureIconProvider _provider = new();

    // ── All built-in icons resolve ────────────────────────────────────────────

    [Theory]
    [InlineData("cloud")]
    [InlineData("database")]
    [InlineData("disk")]
    [InlineData("internet")]
    [InlineData("server")]
    public void GetIcon_BuiltInName_ReturnsIcon(string name)
    {
        var icon = _provider.GetIcon(name);

        Assert.NotNull(icon);
        Assert.Equal(name, icon.Name);
        Assert.Equal("builtin", icon.Pack);
        Assert.Equal("0 0 24 24", icon.ViewBox);
        Assert.False(string.IsNullOrWhiteSpace(icon.SvgContent));
    }

    [Theory]
    [InlineData("Cloud")]
    [InlineData("DATABASE")]
    [InlineData("Server")]
    public void GetIcon_CaseInsensitive(string name)
    {
        var icon = _provider.GetIcon(name);
        Assert.NotNull(icon);
    }

    [Fact]
    public void GetIcon_UnknownName_ReturnsNull()
    {
        Assert.Null(_provider.GetIcon("nonexistent"));
    }

    [Fact]
    public void AvailableIcons_ContainsAllFiveBuiltIns()
    {
        var icons = _provider.AvailableIcons.ToList();

        Assert.Equal(5, icons.Count);
        Assert.Contains("cloud", icons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("database", icons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("disk", icons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("internet", icons, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("server", icons, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("cloud")]
    [InlineData("database")]
    [InlineData("server")]
    public void GetIcon_SvgContentContainsValidElements(string name)
    {
        var icon = _provider.GetIcon(name)!;

        // Each icon should contain at least one SVG shape element.
        bool hasShape = icon.SvgContent.Contains("<path", StringComparison.OrdinalIgnoreCase)
            || icon.SvgContent.Contains("<circle", StringComparison.OrdinalIgnoreCase)
            || icon.SvgContent.Contains("<rect", StringComparison.OrdinalIgnoreCase)
            || icon.SvgContent.Contains("<ellipse", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasShape, $"Icon '{name}' should contain valid SVG shape elements.");
    }
}
