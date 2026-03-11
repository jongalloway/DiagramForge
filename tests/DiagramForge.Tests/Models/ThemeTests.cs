using DiagramForge.Models;

namespace DiagramForge.Tests.Models;

public class ThemeTests
{
    // ── Built-in preset sanity ────────────────────────────────────────────────

    [Fact]
    public void Default_HasNonNullColors()
    {
        var t = Theme.Default;
        AssertValidTheme(t, nameof(Theme.Default));
    }

    [Fact]
    public void Dark_HasNonNullColors()
    {
        var t = Theme.Dark;
        AssertValidTheme(t, nameof(Theme.Dark));
    }

    [Fact]
    public void Neutral_HasNonNullColors()
    {
        var t = Theme.Neutral;
        AssertValidTheme(t, nameof(Theme.Neutral));
    }

    [Fact]
    public void Forest_HasNonNullColors()
    {
        var t = Theme.Forest;
        AssertValidTheme(t, nameof(Theme.Forest));
    }

    [Fact]
    public void Presentation_HasNonNullColors()
    {
        var t = Theme.Presentation;
        AssertValidTheme(t, nameof(Theme.Presentation));
    }

    [Fact]
    public void Dark_BackgroundColor_IsDark()
    {
        // Dark theme should have a dark background (low luminance)
        var (r, g, b) = ColorUtils.ParseHex(Theme.Dark.BackgroundColor);
        double luminance = r * 0.299 + g * 0.587 + b * 0.114;
        Assert.True(luminance < 128, $"Expected dark background, got {Theme.Dark.BackgroundColor} (luminance={luminance:F1})");
    }

    [Fact]
    public void Presentation_FontSize_IsLargerThanDefault()
    {
        Assert.True(Theme.Presentation.FontSize > Theme.Default.FontSize);
        Assert.True(Theme.Presentation.TitleFontSize > Theme.Default.TitleFontSize);
    }

    // ── Built-in themes have NodePalette ──────────────────────────────────────

    [Theory]
    [InlineData("dark")]
    [InlineData("neutral")]
    [InlineData("forest")]
    [InlineData("presentation")]
    public void BuiltInTheme_NodePalette_HasAtLeastSixColors(string themeName)
    {
        var theme = Theme.GetByName(themeName)!;
        Assert.NotNull(theme.NodePalette);
        Assert.True(theme.NodePalette!.Count >= 6,
            $"Expected ≥6 palette colors for '{themeName}', got {theme.NodePalette.Count}");
        foreach (string color in theme.NodePalette)
        {
            Assert.True(color.StartsWith('#'), $"Palette entry '{color}' does not start with '#'");
        }
    }

    // ── GetByName ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("default")]
    [InlineData("dark")]
    [InlineData("neutral")]
    [InlineData("forest")]
    [InlineData("presentation")]
    public void GetByName_KnownName_ReturnsTheme(string name)
    {
        var theme = Theme.GetByName(name);
        Assert.NotNull(theme);
    }

    [Theory]
    [InlineData("DEFAULT")]
    [InlineData("Dark")]
    [InlineData("FOREST")]
    [InlineData("Neutral")]
    [InlineData("PRESENTATION")]
    public void GetByName_CaseInsensitive(string name)
    {
        var theme = Theme.GetByName(name);
        Assert.NotNull(theme);
    }

    [Theory]
    [InlineData("xyz")]
    [InlineData("ocean")]
    [InlineData("")]
    [InlineData("  ")]
    public void GetByName_UnknownName_ReturnsNull(string name)
    {
        var theme = Theme.GetByName(name);
        Assert.Null(theme);
    }

    [Fact]
    public void GetByName_NullInput_ReturnsNull()
    {
        var theme = Theme.GetByName(null!);
        Assert.Null(theme);
    }

    // ── JSON round-trip ───────────────────────────────────────────────────────

    [Fact]
    public void ToJson_FromJson_RoundTrips()
    {
        var original = Theme.Dark;
        string json = original.ToJson();
        var restored = Theme.FromJson(json);

        Assert.Equal(original.BackgroundColor, restored.BackgroundColor);
        Assert.Equal(original.TextColor, restored.TextColor);
        Assert.Equal(original.NodeFillColor, restored.NodeFillColor);
        Assert.Equal(original.FontSize, restored.FontSize);
    }

    [Fact]
    public void FromJson_InvalidJson_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Theme.FromJson("not json"));
    }

    [Fact]
    public void FromJson_NullOrEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Theme.FromJson(""));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void AssertValidTheme(Theme t, string name)
    {
        Assert.False(string.IsNullOrEmpty(t.BackgroundColor),  $"{name}.BackgroundColor is empty");
        Assert.False(string.IsNullOrEmpty(t.NodeFillColor),    $"{name}.NodeFillColor is empty");
        Assert.False(string.IsNullOrEmpty(t.NodeStrokeColor),  $"{name}.NodeStrokeColor is empty");
        Assert.False(string.IsNullOrEmpty(t.EdgeColor),        $"{name}.EdgeColor is empty");
        Assert.False(string.IsNullOrEmpty(t.TextColor),        $"{name}.TextColor is empty");
        Assert.False(string.IsNullOrEmpty(t.FontFamily),       $"{name}.FontFamily is empty");
        Assert.True(t.FontSize > 0,                            $"{name}.FontSize must be > 0");
        Assert.True(t.BorderRadius >= 0,                       $"{name}.BorderRadius must be >= 0");
        Assert.True(t.DiagramPadding > 0,                      $"{name}.DiagramPadding must be > 0");
    }
}
