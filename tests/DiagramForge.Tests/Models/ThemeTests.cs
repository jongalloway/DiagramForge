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

    [Fact]
    public void Presentation_EnablesNodeShadows_WithStrongerSoftShadow()
    {
        Assert.True(Theme.Presentation.UseNodeShadows);
        Assert.Equal("soft", Theme.Presentation.ShadowStyle);
        Assert.InRange(Theme.Presentation.ShadowOpacity, 0.15, 0.18);
        Assert.InRange(Theme.Presentation.ShadowBlur, 1.60, 1.70);
        Assert.InRange(Theme.Presentation.ShadowOffsetY, 1.50, 1.60);
    }

    [Fact]
    public void AngledLight_EnablesNodeShadows_WithStrongerSoftShadow()
    {
        Assert.True(Theme.AngledLight.UseNodeShadows);
        Assert.Equal("soft", Theme.AngledLight.ShadowStyle);
        Assert.InRange(Theme.AngledLight.ShadowOpacity, 0.15, 0.18);
        Assert.InRange(Theme.AngledLight.ShadowBlur, 1.60, 1.70);
        Assert.InRange(Theme.AngledLight.ShadowOffsetY, 1.50, 1.60);
    }

    // ── Built-in themes have NodePalette ──────────────────────────────────────

    [Theory]
    [InlineData("dark")]
    [InlineData("zinc-light")]
    [InlineData("zinc-dark")]
    [InlineData("neutral")]
    [InlineData("forest")]
    [InlineData("presentation")]
    [InlineData("prism")]
    [InlineData("angled-light")]
    [InlineData("angled-dark")]
    [InlineData("github-light")]
    [InlineData("github-dark")]
    [InlineData("tokyo-night")]
    [InlineData("tokyo-night-storm")]
    [InlineData("tokyo-night-light")]
    [InlineData("nord-light")]
    [InlineData("catppuccin-mocha")]
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
    [InlineData("zinc-light")]
    [InlineData("zinc-dark")]
    [InlineData("dark")]
    [InlineData("neutral")]
    [InlineData("forest")]
    [InlineData("presentation")]
    [InlineData("prism")]
    [InlineData("angled-light")]
    [InlineData("angled-dark")]
    [InlineData("github-light")]
    [InlineData("github-dark")]
    [InlineData("nord")]
    [InlineData("nord-light")]
    [InlineData("dracula")]
    [InlineData("tokyo-night")]
    [InlineData("tokyo-night-storm")]
    [InlineData("tokyo-night-light")]
    [InlineData("catppuccin-latte")]
    [InlineData("catppuccin-mocha")]
    [InlineData("solarized-light")]
    [InlineData("solarized-dark")]
    [InlineData("one-dark")]
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
    [InlineData("Tokyo-Night-Light")]
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
        var original = Theme.TokyoNight;
        string json = original.ToJson();
        var restored = Theme.FromJson(json);

        Assert.Equal(original.BackgroundColor, restored.BackgroundColor);
        Assert.Equal(original.TextColor, restored.TextColor);
        Assert.Equal(original.NodeFillColor, restored.NodeFillColor);
        Assert.Equal(original.FontSize, restored.FontSize);
        Assert.Equal(original.GroupFillColor, restored.GroupFillColor);
        Assert.Equal(original.UseGradients, restored.UseGradients);
        Assert.Equal(original.UseBorderGradients, restored.UseBorderGradients);
        Assert.Equal(original.ShadowStyle, restored.ShadowStyle);
        Assert.Equal(original.ShadowOpacity, restored.ShadowOpacity);
    }

    [Fact]
    public void ToJson_FromJson_RoundTripsTransparentBackground()
    {
        var original = Theme.Dracula;
        original.TransparentBackground = true;

        string json = original.ToJson();
        var restored = Theme.FromJson(json);

        Assert.True(restored.TransparentBackground);
    }

    [Fact]
    public void BuiltInThemeNames_ContainsMermaidCoreAndDiagramForgeExclusiveThemes()
    {
        Assert.Contains("default", Theme.BuiltInThemeNames);
        Assert.Contains("dark", Theme.BuiltInThemeNames);
        Assert.Contains("neutral", Theme.BuiltInThemeNames);
        Assert.Contains("forest", Theme.BuiltInThemeNames);
        Assert.Contains("prism", Theme.BuiltInThemeNames);
        Assert.Contains("angled-light", Theme.BuiltInThemeNames);
        Assert.Contains("angled-dark", Theme.BuiltInThemeNames);
        Assert.Contains("tokyo-night", Theme.BuiltInThemeNames);
        Assert.Contains("tokyo-night-storm", Theme.BuiltInThemeNames);
        Assert.Contains("tokyo-night-light", Theme.BuiltInThemeNames);
        Assert.Contains("zinc-light", Theme.BuiltInThemeNames);
        Assert.Contains("zinc-dark", Theme.BuiltInThemeNames);
        Assert.Contains("nord-light", Theme.BuiltInThemeNames);
        Assert.Contains("github-dark", Theme.BuiltInThemeNames);
    }

    [Fact]
    public void Prism_UsesWhiteNodeFills()
    {
        var theme = Theme.Prism;

        Assert.Equal("#FFFFFF", theme.NodeFillColor);
        Assert.NotNull(theme.NodePalette);
        Assert.All(theme.NodePalette!, color => Assert.Equal("#FFFFFF", color));
        Assert.NotNull(theme.BorderGradientStops);
        Assert.True(theme.BorderGradientStops!.Count >= 4);
    }

    [Fact]
    public void AngledLight_UsesStrongerDiagonalFillDefaults()
    {
        var theme = Theme.AngledLight;

        Assert.True(theme.UseGradients);
        Assert.False(theme.UseBorderGradients);
        Assert.InRange(theme.GradientStrength, 0.14, 0.16);
        Assert.Equal("soft", theme.ShadowStyle);
    }

    [Fact]
    public void AngledDark_UsesDiagonalFillOnDarkSurface()
    {
        var theme = Theme.AngledDark;

        Assert.True(theme.UseGradients);
        Assert.False(theme.UseBorderGradients);
        Assert.True(ColorUtils.GetLuminance(theme.BackgroundColor) < 128);
        Assert.InRange(theme.GradientStrength, 0.14, 0.16);
    }

    [Theory]
    [InlineData("presentation")]
    [InlineData("forest")]
    [InlineData("github-dark")]
    [InlineData("dracula")]
    [InlineData("tokyo-night")]
    public void BuiltInTheme_SelectedThemes_UseExpressiveMultiStopBorderGradients(string name)
    {
        var theme = Theme.GetByName(name)!;

        Assert.True(theme.UseBorderGradients);
        Assert.NotNull(theme.BorderGradientStops);
        Assert.True(theme.BorderGradientStops!.Count >= 4);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("dark")]
    [InlineData("nord")]
    [InlineData("one-dark")]
    public void BuiltInTheme_NonExpressiveGradientThemes_DefaultToSubtleBorderGradients(string name)
    {
        var theme = Theme.GetByName(name)!;

        Assert.True(theme.UseBorderGradients);
        Assert.Null(theme.BorderGradientStops);
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
        Assert.False(string.IsNullOrEmpty(t.BackgroundColor), $"{name}.BackgroundColor is empty");
        Assert.False(string.IsNullOrEmpty(t.NodeFillColor), $"{name}.NodeFillColor is empty");
        Assert.False(string.IsNullOrEmpty(t.NodeStrokeColor), $"{name}.NodeStrokeColor is empty");
        Assert.False(string.IsNullOrEmpty(t.EdgeColor), $"{name}.EdgeColor is empty");
        Assert.False(string.IsNullOrEmpty(t.TextColor), $"{name}.TextColor is empty");
        Assert.False(string.IsNullOrEmpty(t.FontFamily), $"{name}.FontFamily is empty");
        Assert.True(t.FontSize > 0, $"{name}.FontSize must be > 0");
        Assert.True(t.BorderRadius >= 0, $"{name}.BorderRadius must be >= 0");
        Assert.True(t.DiagramPadding > 0, $"{name}.DiagramPadding must be > 0");
    }
}
