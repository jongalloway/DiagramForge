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

    [Fact]
    public void Lighten_WithAlpha_PreservesAlphaChannel()
    {
        // #4F81BDCC — alpha = CC = 204
        string result = ColorUtils.Lighten("#4F81BDCC", 0.5);
        var (_, _, _, a) = ColorUtils.ParseHexWithAlpha(result);
        Assert.Equal(0xCC, a);
        Assert.Equal(9, result.Length); // #RRGGBBAA
    }

    [Fact]
    public void Lighten_WithShorthandAlpha_PreservesAlpha()
    {
        // #48FC = #4488FFCC
        string result = ColorUtils.Lighten("#48FC", 0.0);
        var (r, g, b, a) = ColorUtils.ParseHexWithAlpha(result);
        Assert.Equal(0x44, r);
        Assert.Equal(0x88, g);
        Assert.Equal(0xFF, b);
        Assert.Equal(0xCC, a);
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

    [Fact]
    public void Darken_WithAlpha_PreservesAlphaChannel()
    {
        string result = ColorUtils.Darken("#4F81BD80", 0.5);
        var (_, _, _, a) = ColorUtils.ParseHexWithAlpha(result);
        Assert.Equal(0x80, a);
        Assert.Equal(9, result.Length); // #RRGGBBAA
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

    [Fact]
    public void Desaturate_WithAlpha_PreservesAlphaChannel()
    {
        string result = ColorUtils.Desaturate("#4F81BD40", 1.0);
        var (_, _, _, a) = ColorUtils.ParseHexWithAlpha(result);
        Assert.Equal(0x40, a);
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
    public void ParseHex_EightDigit_StripsAlpha()
    {
        // #4F81BDCC — alpha CC should be discarded
        var (r, g, b) = ColorUtils.ParseHex("#4F81BDCC");
        Assert.Equal(0x4F, r);
        Assert.Equal(0x81, g);
        Assert.Equal(0xBD, b);
    }

    [Fact]
    public void ParseHex_FourDigitShorthand_ExpandsAndStripsAlpha()
    {
        // #F80C → #FF8800CC; alpha discarded
        var (r, g, b) = ColorUtils.ParseHex("#F80C");
        Assert.Equal(0xFF, r);
        Assert.Equal(0x88, g);
        Assert.Equal(0x00, b);
    }

    [Fact]
    public void ParseHex_InvalidFormat_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ColorUtils.ParseHex("notacolor"));
    }

    [Fact]
    public void ParseHex_BareHexWithoutHash_ThrowsArgumentException()
    {
        // Bare hex strings (no leading '#') must be rejected
        Assert.Throws<ArgumentException>(() => ColorUtils.ParseHex("4F81BD"));
    }

    // ── ParseHexWithAlpha ─────────────────────────────────────────────────────

    [Fact]
    public void ParseHexWithAlpha_SixDigit_AlphaIsFullyOpaque()
    {
        var (_, _, _, a) = ColorUtils.ParseHexWithAlpha("#4F81BD");
        Assert.Equal(255, a);
    }

    [Fact]
    public void ParseHexWithAlpha_EightDigit_ParsesAlphaCorrectly()
    {
        var (r, g, b, a) = ColorUtils.ParseHexWithAlpha("#4F81BDCC");
        Assert.Equal(0x4F, r);
        Assert.Equal(0x81, g);
        Assert.Equal(0xBD, b);
        Assert.Equal(0xCC, a);
    }

    [Fact]
    public void ParseHexWithAlpha_FourDigitShorthand_ExpandsCorrectly()
    {
        // #F80C → #FF8800CC
        var (r, g, b, a) = ColorUtils.ParseHexWithAlpha("#F80C");
        Assert.Equal(0xFF, r);
        Assert.Equal(0x88, g);
        Assert.Equal(0x00, b);
        Assert.Equal(0xCC, a);
    }

    [Fact]
    public void ParseHexWithAlpha_ThreeDigitShorthand_AlphaIsFullyOpaque()
    {
        var (r, g, b, a) = ColorUtils.ParseHexWithAlpha("#F80");
        Assert.Equal(0xFF, r);
        Assert.Equal(0x88, g);
        Assert.Equal(0x00, b);
        Assert.Equal(255, a);
    }

    [Fact]
    public void ParseHexWithAlpha_InvalidFormat_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ColorUtils.ParseHexWithAlpha("notacolor"));
    }

    // ── Fully opaque alpha omitted from output ────────────────────────────────

    [Fact]
    public void Lighten_OpaqueInput_OutputHasNoAlphaSuffix()
    {
        // When alpha is FF (opaque), output should be #RRGGBB not #RRGGBBFF
        string result = ColorUtils.Lighten("#4F81BDFF", 0.5);
        Assert.Equal(7, result.Length);
    }

    [Fact]
    public void Darken_OpaqueInput_OutputHasNoAlphaSuffix()
    {
        string result = ColorUtils.Darken("#4F81BDFF", 0.5);
        Assert.Equal(7, result.Length);
    }
}

