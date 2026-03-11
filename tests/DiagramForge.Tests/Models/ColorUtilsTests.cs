using DiagramForge.Models;

namespace DiagramForge.Tests.Models;

public class ColorUtilsTests
{
    // ── Lighten ───────────────────────────────────────────────────────────────

    [Fact]
    public void Lighten_ZeroAmount_ReturnsOriginal()
    {
        string result = ColorUtils.Lighten("#4F81BD", 0);
        Assert.Equal("#4F81BD", result, ignoreCase: true);
    }

    [Fact]
    public void Lighten_FullAmount_ReturnsWhite()
    {
        string result = ColorUtils.Lighten("#4F81BD", 1.0);
        Assert.Equal("#FFFFFF", result, ignoreCase: true);
    }

    [Fact]
    public void Lighten_HalfAmount_ProducesLighterColor()
    {
        var (r0, g0, b0) = ColorUtils.ParseHex("#4F81BD");
        string lighter = ColorUtils.Lighten("#4F81BD", 0.5);
        var (r1, g1, b1) = ColorUtils.ParseHex(lighter);

        Assert.True(r1 > r0 || g1 > g0 || b1 > b0, "Lightened color should have higher channel values.");
    }

    // ── Darken ────────────────────────────────────────────────────────────────

    [Fact]
    public void Darken_ZeroAmount_ReturnsOriginal()
    {
        string result = ColorUtils.Darken("#4F81BD", 0);
        Assert.Equal("#4F81BD", result, ignoreCase: true);
    }

    [Fact]
    public void Darken_FullAmount_ReturnsBlack()
    {
        string result = ColorUtils.Darken("#4F81BD", 1.0);
        Assert.Equal("#000000", result, ignoreCase: true);
    }

    [Fact]
    public void Darken_HalfAmount_ProducesDarkerColor()
    {
        var (r0, g0, b0) = ColorUtils.ParseHex("#4F81BD");
        string darker = ColorUtils.Darken("#4F81BD", 0.5);
        var (r1, g1, b1) = ColorUtils.ParseHex(darker);

        Assert.True(r1 < r0 || g1 < g0 || b1 < b0, "Darkened color should have lower channel values.");
    }

    // ── Desaturate ────────────────────────────────────────────────────────────

    [Fact]
    public void Desaturate_ZeroAmount_ReturnsOriginal()
    {
        string result = ColorUtils.Desaturate("#4F81BD", 0);
        Assert.Equal("#4F81BD", result, ignoreCase: true);
    }

    [Fact]
    public void Desaturate_FullAmount_ProducesGrayscale()
    {
        string result = ColorUtils.Desaturate("#4F81BD", 1.0);
        var (r, g, b) = ColorUtils.ParseHex(result);
        // All channels should be equal (grayscale)
        Assert.Equal(r, g);
        Assert.Equal(g, b);
    }

    // ── ParseHex ──────────────────────────────────────────────────────────────

    [Fact]
    public void ParseHex_SixDigit_ParsesCorrectly()
    {
        var (r, g, b) = ColorUtils.ParseHex("#4F81BD");
        Assert.Equal(0x4F, r);
        Assert.Equal(0x81, g);
        Assert.Equal(0xBD, b);
    }

    [Fact]
    public void ParseHex_ThreeDigitShorthand_ExpandsCorrectly()
    {
        // #F80 == #FF8800
        var (r, g, b) = ColorUtils.ParseHex("#F80");
        Assert.Equal(0xFF, r);
        Assert.Equal(0x88, g);
        Assert.Equal(0x00, b);
    }

    [Fact]
    public void ParseHex_InvalidFormat_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ColorUtils.ParseHex("notacolor"));
    }
}
