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

    // ── GetRelativeLuminance ──────────────────────────────────────────────────

    [Fact]
    public void GetRelativeLuminance_White_ReturnsOne()
    {
        double l = ColorUtils.GetRelativeLuminance("#FFFFFF");
        Assert.Equal(1.0, l, precision: 6);
    }

    [Fact]
    public void GetRelativeLuminance_Black_ReturnsZero()
    {
        double l = ColorUtils.GetRelativeLuminance("#000000");
        Assert.Equal(0.0, l, precision: 6);
    }

    [Fact]
    public void GetRelativeLuminance_MidGray_IsGammaCorrectedBelowLinearHalf()
    {
        // sRGB #7F7F7F encodes ~50% brightness, but sRGB gamma correction places
        // its WCAG relative luminance around 0.216 — well below 0.5.
        double l = ColorUtils.GetRelativeLuminance("#7F7F7F");
        Assert.InRange(l, 0.20, 0.25);
    }

    [Fact]
    public void GetRelativeLuminance_IsHigherForLightColors()
    {
        double lLight = ColorUtils.GetRelativeLuminance("#F0F0F0");
        double lDark = ColorUtils.GetRelativeLuminance("#202020");
        Assert.True(lLight > lDark);
    }

    // ── GetContrastRatio ──────────────────────────────────────────────────────

    [Fact]
    public void GetContrastRatio_BlackOnWhite_Returns21()
    {
        double ratio = ColorUtils.GetContrastRatio("#000000", "#FFFFFF");
        Assert.Equal(21.0, ratio, precision: 4);
    }

    [Fact]
    public void GetContrastRatio_SameColor_Returns1()
    {
        double ratio = ColorUtils.GetContrastRatio("#4F81BD", "#4F81BD");
        Assert.Equal(1.0, ratio, precision: 4);
    }

    [Fact]
    public void GetContrastRatio_IsSymmetric()
    {
        double r1 = ColorUtils.GetContrastRatio("#1F2937", "#FFFFFF");
        double r2 = ColorUtils.GetContrastRatio("#FFFFFF", "#1F2937");
        Assert.Equal(r1, r2, precision: 6);
    }

    [Fact]
    public void GetContrastRatio_DarkTextOnWhite_MeetsWcagAA()
    {
        // #1F2937 (very dark gray) on white should comfortably exceed 4.5:1
        double ratio = ColorUtils.GetContrastRatio("#1F2937", "#FFFFFF");
        Assert.True(ratio >= 4.5, $"Expected ≥4.5:1 WCAG AA contrast, got {ratio:F2}:1");
    }

    [Fact]
    public void GetContrastRatio_LightTextOnDarkBackground_MeetsWcagAA()
    {
        // #E2E8F0 (near-white) on #0F172A (very dark navy) should exceed 4.5:1
        double ratio = ColorUtils.GetContrastRatio("#E2E8F0", "#0F172A");
        Assert.True(ratio >= 4.5, $"Expected ≥4.5:1 WCAG AA contrast, got {ratio:F2}:1");
    }

    [Fact]
    public void GetContrastRatio_AlwaysReturnsValueBetween1And21()
    {
        double ratio = ColorUtils.GetContrastRatio("#4F81BD", "#E8F0FC");
        Assert.InRange(ratio, 1.0, 21.0);
    }

    // ── IsLight / ChooseTextColor ─────────────────────────────────────────────

    [Fact]
    public void GetLuminance_White_ReturnsMaxBt601()
    {
        double l = ColorUtils.GetLuminance("#FFFFFF");
        Assert.Equal(255.0, l, precision: 4);
    }

    [Fact]
    public void GetLuminance_Black_ReturnsZero()
    {
        double l = ColorUtils.GetLuminance("#000000");
        Assert.Equal(0.0, l, precision: 4);
    }

    [Fact]
    public void IsLight_White_ReturnsTrue()
    {
        Assert.True(ColorUtils.IsLight("#FFFFFF"));
    }

    [Fact]
    public void IsLight_Black_ReturnsFalse()
    {
        Assert.False(ColorUtils.IsLight("#000000"));
    }

    [Fact]
    public void ChooseTextColor_DarkBackground_ReturnsLightText()
    {
        string chosen = ColorUtils.ChooseTextColor("#111827");
        Assert.Equal("#F8FAFC", chosen);
    }

    [Fact]
    public void ChooseTextColor_LightBackground_ReturnsDarkText()
    {
        string chosen = ColorUtils.ChooseTextColor("#FFFFFF");
        Assert.Equal("#0F172A", chosen);
    }

    // ── ToHex ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ToHex_OpaqueChannels_ReturnsRrggbbFormat()
    {
        string result = ColorUtils.ToHex(0x4F, 0x81, 0xBD);
        Assert.Equal("#4F81BD", result, ignoreCase: true);
        Assert.Equal(7, result.Length);
    }

    [Fact]
    public void ToHex_WithNonOpaqueAlpha_ReturnsRrggbbaaFormat()
    {
        string result = ColorUtils.ToHex(0x4F, 0x81, 0xBD, 0xCC);
        Assert.Equal("#4F81BDCC", result, ignoreCase: true);
        Assert.Equal(9, result.Length);
    }

    [Fact]
    public void ToHex_OpaqueAlpha255_OmitsAlphaByte()
    {
        string result = ColorUtils.ToHex(100, 150, 200, 255);
        Assert.Equal(7, result.Length); // #RRGGBB only
    }

    [Fact]
    public void ToHex_OverflowChannels_ClampsTo255()
    {
        // Values above 255 should clamp — not produce out-of-range hex
        string result = ColorUtils.ToHex(300, 400, 500);
        Assert.Equal("#FFFFFF", result, ignoreCase: true);
    }

    [Fact]
    public void ToHex_NegativeChannels_ClampsToZero()
    {
        string result = ColorUtils.ToHex(-10, -50, -100);
        Assert.Equal("#000000", result, ignoreCase: true);
    }

    [Fact]
    public void ToHex_MixedOutOfRangeChannels_ClampedIndividually()
    {
        // r=300→FF, g=0x81 stays, b=-1→00
        string result = ColorUtils.ToHex(300, 0x81, -1);
        Assert.Equal("#FF8100", result, ignoreCase: true);
    }

    // ── Vibrant ───────────────────────────────────────────────────────────────

    [Fact]
    public void Vibrant_PastelInput_ProducesMoreSaturatedResult()
    {
        // A pastel blue with clear hue deviation across channels
        string pastel = "#AACCEE";
        string vibrant = ColorUtils.Vibrant(pastel);
        var (pr, pg, pb) = ColorUtils.ParseHex(pastel);
        var (vr, vg, vb) = ColorUtils.ParseHex(vibrant);

        // The max-min spread (saturation proxy) should be larger after vibrant
        int pastelSpread = Math.Max(pr, Math.Max(pg, pb)) - Math.Min(pr, Math.Min(pg, pb));
        int vibrantSpread = Math.Max(vr, Math.Max(vg, vb)) - Math.Min(vr, Math.Min(vg, vb));
        Assert.True(vibrantSpread > pastelSpread, $"Expected vibrant spread ({vibrantSpread}) > pastel spread ({pastelSpread}).");
    }

    [Fact]
    public void Vibrant_PreservesAlphaChannel()
    {
        string result = ColorUtils.Vibrant("#AACCEECC");
        var (_, _, _, a) = ColorUtils.ParseHexWithAlpha(result);
        Assert.Equal(0xCC, a);
        Assert.Equal(9, result.Length); // #RRGGBBAA
    }

    [Fact]
    public void Vibrant_OpaqueInput_OutputHasNoAlphaSuffix()
    {
        string result = ColorUtils.Vibrant("#AACCEE");
        Assert.Equal(7, result.Length); // #RRGGBB
    }

    [Fact]
    public void Vibrant_ReturnsValidHexString()
    {
        string result = ColorUtils.Vibrant("#B3D9F2");
        // Should start with # and be parseable
        Assert.StartsWith("#", result);
        var (r, g, b) = ColorUtils.ParseHex(result); // must not throw
        Assert.InRange(r, 0, 255);
        Assert.InRange(g, 0, 255);
        Assert.InRange(b, 0, 255);
    }

    [Fact]
    public void Vibrant_HigherAmplify_ProducesMoreIntenseResult()
    {
        string pastel = "#AACCEE";
        string vibrant1 = ColorUtils.Vibrant(pastel, amplify: 2.0);
        string vibrant2 = ColorUtils.Vibrant(pastel, amplify: 4.0);
        var (r1, g1, b1) = ColorUtils.ParseHex(vibrant1);
        var (r2, g2, b2) = ColorUtils.ParseHex(vibrant2);
        int spread1 = Math.Max(r1, Math.Max(g1, b1)) - Math.Min(r1, Math.Min(g1, b1));
        int spread2 = Math.Max(r2, Math.Max(g2, b2)) - Math.Min(r2, Math.Min(g2, b2));
        Assert.True(spread2 >= spread1, "Higher amplify should produce equal or greater channel spread.");
    }
}

