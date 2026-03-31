using DiagramForge.Models;

namespace DiagramForge.Tests.Models;

public class ThemePaletteResolverTests
{
    // ── BuildRingColors — basic contract ──────────────────────────────────────

    [Fact]
    public void BuildRingColors_SingleRing_ReturnsOnlyOuterColor()
    {
        var theme = Theme.Default;
        string outerColor = "#AABBCC";
        string[] result = ThemePaletteResolver.BuildRingColors(theme, 1, "#111111", outerColor, isLightBackground: true);
        Assert.Single(result);
        Assert.Equal(outerColor, result[0], ignoreCase: true);
    }

    [Fact]
    public void BuildRingColors_ReturnsExactlyRingCountColors()
    {
        var theme = Theme.Default;
        for (int count = 1; count <= 5; count++)
        {
            string[] result = ThemePaletteResolver.BuildRingColors(theme, count, "#000000", "#AABBCC", isLightBackground: true);
            Assert.Equal(count, result.Length);
        }
    }

    [Fact]
    public void BuildRingColors_FirstColorIsAlwaysOuterColor()
    {
        var theme = Theme.Default;
        string outerColor = "#123456";
        string[] result = ThemePaletteResolver.BuildRingColors(theme, 3, "#FFFFFF", outerColor, isLightBackground: false);
        Assert.Equal(outerColor, result[0], ignoreCase: true);
    }

    // ── BuildRingColors — visual quality ─────────────────────────────────────

    [Fact]
    public void BuildRingColors_MultipleRings_AllColorsAreValidHex()
    {
        var theme = Theme.Default;
        string[] result = ThemePaletteResolver.BuildRingColors(theme, 4, "#000000", "#AABBCC", isLightBackground: true);
        foreach (string color in result)
        {
            Assert.StartsWith("#", color);
            var (r, g, b) = ColorUtils.ParseHex(color); // must not throw
            Assert.InRange(r, 0, 255);
            Assert.InRange(g, 0, 255);
            Assert.InRange(b, 0, 255);
        }
    }

    [Fact]
    public void BuildRingColors_MultipleRings_AdjacentColorsAreDistinctHues()
    {
        var theme = Theme.Default;
        string[] result = ThemePaletteResolver.BuildRingColors(theme, 4, "#000000", "#AABBCC", isLightBackground: true);

        // Each consecutive pair should have at least some hue separation
        for (int i = 0; i < result.Length - 1; i++)
        {
            double dist = ColorUtils.GetHueDistance(result[i], result[i + 1]);
            Assert.True(dist > 0, $"Colors at index {i} and {i + 1} should have different hues.");
        }
    }

    [Fact]
    public void BuildRingColors_WorksWithDarkBackground()
    {
        var theme = Theme.GetByName("dark") ?? Theme.Default;
        string[] result = ThemePaletteResolver.BuildRingColors(theme, 3, "#111111", "#334455", isLightBackground: false);
        Assert.Equal(3, result.Length);
        foreach (string color in result)
        {
            Assert.StartsWith("#", color);
            ColorUtils.ParseHex(color); // must not throw
        }
    }

    [Fact]
    public void BuildRingColors_WithNodePalette_InfluencesSelection()
    {
        // Use a large ring count so the palette candidates actually get selected.
        // The three palette entries (#F97316 ~28°, #22C55E ~142°, #A855F7 ~287°) span
        // very different hue regions from the base theme, so with-palette colors
        // should differ from without-palette colors for at least one ring slot.
        var themeWithout = Theme.FromPalette("#0EA5E9");
        var themeWith = Theme.FromPalette("#0EA5E9");
        themeWith.NodePalette = ["#F97316", "#22C55E", "#A855F7"];

        string[] without = ThemePaletteResolver.BuildRingColors(themeWithout, 5, "#000000", "#0EA5E9", isLightBackground: false);
        string[] with = ThemePaletteResolver.BuildRingColors(themeWith, 5, "#000000", "#0EA5E9", isLightBackground: false);

        // Both return exactly 5 colors
        Assert.Equal(5, without.Length);
        Assert.Equal(5, with.Length);

        // With highly distinct palette entries at least one ring color should differ
        bool anyDiffers = without.Zip(with).Any(pair =>
            !string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));
        Assert.True(anyDiffers, "NodePalette entries should influence at least one ring color selection.");
    }

    [Fact]
    public void BuildRingColors_PrismTheme_UsesChromaticInnerRingFallback()
    {
        string[] result = ThemePaletteResolver.BuildRingColors(
            Theme.Prism,
            ringCount: 5,
            centerColor: "#FFFFFF",
            outerColor: "#D6DEE8",
            isLightBackground: true);

        Assert.Equal(5, result.Length);
        Assert.Equal("#D6DEE8", result[0], ignoreCase: true);

        string[] innerRings = result.Skip(1).ToArray();
        Assert.All(innerRings, color => Assert.False(ColorUtils.IsAchromatic(color)));
        Assert.False(ColorUtils.IsPaletteMonochrome(innerRings, Theme.Prism.BackgroundColor),
            "Prism inner ring colors should come from a chromatic fallback palette rather than stay monochrome.");
    }

    // ── BuildRingColors — fallback path ───────────────────────────────────────

    [Fact]
    public void BuildRingColors_VeryHighRingCount_StillReturnsCorrectCount()
    {
        // Force fallback by asking for many more rings than candidate pool can supply
        var theme = Theme.Default;
        const int ringCount = 12;
        string[] result = ThemePaletteResolver.BuildRingColors(theme, ringCount, "#000000", "#AABBCC", isLightBackground: true);
        Assert.Equal(ringCount, result.Length);
        foreach (string color in result)
        {
            Assert.StartsWith("#", color);
            ColorUtils.ParseHex(color); // must not throw
        }
    }

    // ── BuildRingColors — argument guards ─────────────────────────────────────

    [Fact]
    public void BuildRingColors_ZeroRingCount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ThemePaletteResolver.BuildRingColors(Theme.Default, 0, "#000000", "#AABBCC", isLightBackground: true));
    }

    [Fact]
    public void BuildRingColors_NegativeRingCount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ThemePaletteResolver.BuildRingColors(Theme.Default, -1, "#000000", "#AABBCC", isLightBackground: true));
    }

    [Fact]
    public void BuildRingColors_NullTheme_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ThemePaletteResolver.BuildRingColors(null!, 3, "#000000", "#AABBCC", isLightBackground: true));
    }

    // ── ResolveEffectivePalette ───────────────────────────────────────────────

    [Fact]
    public void ResolveEffectivePalette_NullTheme_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ThemePaletteResolver.ResolveEffectivePalette(null!));
    }

    [Fact]
    public void ResolveEffectivePalette_NormalChromaticPalette_ReturnsPaletteUnchanged()
    {
        var theme = Theme.Default;
        var result = ThemePaletteResolver.ResolveEffectivePalette(theme);
        // Default theme has a chromatic palette — it should be returned as-is
        Assert.NotNull(result);
        Assert.True(result.Count > 0);
        // The result should be the same reference as NodePalette (no copy made)
        Assert.Same(theme.NodePalette, result);
    }

    [Fact]
    public void ResolveEffectivePalette_AllWhitePalette_ReturnsChromaticFallback()
    {
        var theme = Theme.FromPalette("#3B82F6");
        theme.NodePalette = ["#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF",
                             "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF"];

        var result = ThemePaletteResolver.ResolveEffectivePalette(theme);

        Assert.Equal(8, result.Count);
        Assert.False(ColorUtils.IsPaletteMonochrome(result),
            "Fallback palette should not be monochrome.");
    }

    [Fact]
    public void ResolveEffectivePalette_AllBlackPalette_ReturnsChromaticFallback()
    {
        var theme = Theme.FromPalette("#3B82F6");
        theme.NodePalette = ["#000000", "#000000", "#000000"];

        var result = ThemePaletteResolver.ResolveEffectivePalette(theme);

        Assert.Equal(8, result.Count);
        Assert.False(ColorUtils.IsPaletteMonochrome(result),
            "Fallback palette should not be monochrome.");
    }

    [Fact]
    public void ResolveEffectivePalette_PrismTheme_ReturnsChromaticFallback()
    {
        // Prism has all-white NodePalette but has chromatic BorderGradientStops
        var result = ThemePaletteResolver.ResolveEffectivePalette(Theme.Prism);

        Assert.Equal(8, result.Count);
        // Each color should be valid hex
        foreach (string color in result)
        {
            Assert.StartsWith("#", color);
            ColorUtils.ParseHex(color); // must not throw
        }
        // The fallback from gradient stops should not be monochrome
        Assert.False(ColorUtils.IsPaletteMonochrome(result),
            "Prism fallback palette derived from gradient stops should not be monochrome.");
    }

    [Fact]
    public void ResolveEffectivePalette_PrismTheme_WithRequestedCount_ReturnsChromaticFallbackOfRequestedSize()
    {
        var result = ThemePaletteResolver.ResolveEffectivePalette(Theme.Prism, desiredCount: 12);

        Assert.Equal(12, result.Count);
        Assert.Equal(Theme.Prism.BorderGradientStops![0], result[0], ignoreCase: true);
        Assert.Equal(Theme.Prism.BorderGradientStops[^1], result[^1], ignoreCase: true);
        Assert.False(ColorUtils.IsPaletteMonochrome(result, Theme.Prism.BackgroundColor));
    }

    [Fact]
    public void ResolveEffectivePalette_MonochromeNoGradientStops_UsesHueRotationFallback()
    {
        var theme = Theme.FromPalette("#E84393");
        theme.NodePalette = ["#FFFFFF", "#FFFFFF", "#FFFFFF"];
        theme.UseBorderGradients = false;
        theme.BorderGradientStops = null;

        var result = ThemePaletteResolver.ResolveEffectivePalette(theme);

        Assert.Equal(8, result.Count);
        Assert.False(ColorUtils.IsPaletteMonochrome(result),
            "Hue-rotation fallback palette should not be monochrome.");
    }

    [Fact]
    public void ResolveEffectivePalette_WithGradientStops_UsesGradientStopsFallback()
    {
        var theme = Theme.FromPalette("#3B82F6");
        theme.NodePalette = ["#FFFFFF", "#FFFFFF", "#FFFFFF"];
        theme.UseBorderGradients = true;
        theme.BorderGradientStops = ["#2563EB", "#7C3AED", "#DB2777", "#F59E0B"];

        var result = ThemePaletteResolver.ResolveEffectivePalette(theme);

        // Must have exactly 8 entries
        Assert.Equal(8, result.Count);
        // First entry should be at or near the first gradient stop
        Assert.Equal(theme.BorderGradientStops[0], result[0], ignoreCase: true);
        // Last entry should be at or near the last gradient stop
        Assert.Equal(theme.BorderGradientStops[^1], result[^1], ignoreCase: true);
    }

    [Fact]
    public void ResolveEffectivePalette_FallbackPalette_AllColorsAreValidHex()
    {
        var theme = Theme.FromPalette("#6366F1");
        theme.NodePalette = ["#FFFFFF", "#FFFFFF", "#FFFFFF"];
        theme.UseBorderGradients = false;

        var result = ThemePaletteResolver.ResolveEffectivePalette(theme);

        foreach (string color in result)
        {
            Assert.StartsWith("#", color);
            var (r, g, b) = ColorUtils.ParseHex(color);
            Assert.InRange(r, 0, 255);
            Assert.InRange(g, 0, 255);
            Assert.InRange(b, 0, 255);
        }
    }

    [Fact]
    public void ResolveEffectivePalette_MixedMonochromeGrays_ReturnsFallback()
    {
        var theme = Theme.FromPalette("#3B82F6");
        theme.NodePalette = ["#FFFFFF", "#D3D3D3", "#C0C0C0", "#808080"];
        theme.UseBorderGradients = false;

        var result = ThemePaletteResolver.ResolveEffectivePalette(theme);

        Assert.Equal(8, result.Count);
        Assert.False(ColorUtils.IsPaletteMonochrome(result),
            "Fallback for mixed-gray palette should not be monochrome.");
    }

    [Fact]
    public void ResolveEffectivePalette_MonochromeGradientStops_FallsBackToHueRotation()
    {
        // Even when UseBorderGradients is true, if the gradient stops are themselves
        // monochrome the method should skip them and fall back to hue rotation.
        var theme = Theme.FromPalette("#3B82F6");
        theme.NodePalette = ["#FFFFFF", "#FFFFFF"];
        theme.UseBorderGradients = true;
        theme.BorderGradientStops = ["#808080", "#AAAAAA"];  // achromatic stops

        var result = ThemePaletteResolver.ResolveEffectivePalette(theme);

        Assert.Equal(8, result.Count);
        Assert.False(ColorUtils.IsPaletteMonochrome(result),
            "Result should be chromatic even when gradient stops are monochrome.");
    }
}
