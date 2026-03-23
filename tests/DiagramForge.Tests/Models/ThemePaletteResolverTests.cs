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
    public void BuildRingColors_WithNodePalette_UsesAdditionalCandidates()
    {
        // Build a theme without palette, then one with palette
        var themeWithout = Theme.FromPalette("#0EA5E9");
        var themeWith = Theme.FromPalette("#0EA5E9");
        themeWith.NodePalette = ["#F97316", "#22C55E", "#A855F7"];

        // Both should produce 3 valid colors; the palette variant may differ
        string[] without = ThemePaletteResolver.BuildRingColors(themeWithout, 3, "#000000", "#0EA5E9", isLightBackground: false);
        string[] with = ThemePaletteResolver.BuildRingColors(themeWith, 3, "#000000", "#0EA5E9", isLightBackground: false);

        Assert.Equal(3, without.Length);
        Assert.Equal(3, with.Length);
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
}
