using DiagramForge.Models;

namespace DiagramForge.Tests.Models;

public class ThemeFromPaletteTests
{
    [Fact]
    public void FromPalette_PrimaryOnly_ProducesUsableTheme()
    {
        var theme = Theme.FromPalette("#4F81BD");

        Assert.Equal("#4F81BD", theme.PrimaryColor);
        Assert.False(string.IsNullOrEmpty(theme.BackgroundColor));
        Assert.False(string.IsNullOrEmpty(theme.NodeFillColor));
        Assert.False(string.IsNullOrEmpty(theme.EdgeColor));
        Assert.False(string.IsNullOrEmpty(theme.TextColor));
    }

    [Fact]
    public void FromPalette_AllParameters_UsesProvidedValues()
    {
        var theme = Theme.FromPalette(
            primaryColor:     "#FF0000",
            secondaryColor:   "#00FF00",
            accentColor:      "#0000FF",
            backgroundColor:  "#111111");

        Assert.Equal("#FF0000", theme.PrimaryColor);
        Assert.Equal("#00FF00", theme.SecondaryColor);
        Assert.Equal("#0000FF", theme.AccentColor);
        Assert.Equal("#111111", theme.BackgroundColor);
    }

    [Fact]
    public void FromPalette_GeneratesNodePalette_WithAtLeastSixColors()
    {
        var theme = Theme.FromPalette("#4F81BD", "#70AD47", "#ED7D31");

        Assert.NotNull(theme.NodePalette);
        Assert.True(theme.NodePalette!.Count >= 6,
            $"Expected ≥6 palette colors, got {theme.NodePalette.Count}");
    }

    [Fact]
    public void FromPalette_NodePaletteColors_AreValidHex()
    {
        var theme = Theme.FromPalette("#4F81BD");
        Assert.NotNull(theme.NodePalette);
        foreach (string color in theme.NodePalette!)
        {
            Assert.True(color.StartsWith('#'), $"'{color}' does not start with '#'");
            Assert.True(color.Length is 7, $"'{color}' length is not 7 (#RRGGBB)");
        }
    }

    [Fact]
    public void FromPalette_DarkBackground_SetsDarkTextColor()
    {
        var theme = Theme.FromPalette("#5B9BD5", backgroundColor: "#1E1E1E");

        // On dark background the text should be light
        var (r, g, b) = ColorUtils.ParseHex(theme.TextColor);
        double luminance = r * 0.299 + g * 0.587 + b * 0.114;
        Assert.True(luminance > 128, $"Expected light text on dark background, got {theme.TextColor} (luminance={luminance:F1})");
    }

    [Fact]
    public void FromPalette_NullPrimary_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => Theme.FromPalette(null!));
    }

    [Fact]
    public void FromPalette_EmptyPrimary_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Theme.FromPalette(""));
    }

    [Fact]
    public void FromPalette_NullBackground_DefaultsToWhite()
    {
        var theme = Theme.FromPalette("#4F81BD", backgroundColor: null);
        Assert.Equal("#FFFFFF", theme.BackgroundColor);
    }
}
