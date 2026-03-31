namespace DiagramForge.Models;

/// <summary>
/// Shared helpers for deriving diagram color palettes from a <see cref="Theme"/>.
/// Diagram-specific layout code delegates color policy here so the underlying
/// hue rotation, hue-distance scoring, and palette derivation logic can be
/// reused across diagram kinds.
/// </summary>
public static class ThemePaletteResolver
{
    private const int DefaultPaletteSize = 8;

    /// <summary>
    /// Returns a palette suitable for direct node-fill use, regardless of whether
    /// the theme's <see cref="Theme.NodePalette"/> is chromatic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <see cref="Theme.NodePalette"/> is non-empty and
    /// <see cref="ColorUtils.IsPaletteMonochrome"/> returns <see langword="false"/>,
    /// the palette is returned unchanged.
    /// </para>
    /// <para>
    /// When the palette is monochrome (all-white, all-black, all-same, or all matching
    /// the background), a fallback palette is built:
    /// <list type="number">
    ///   <item>
    ///     If <see cref="Theme.UseBorderGradients"/> is <see langword="true"/> and
    ///     <see cref="Theme.BorderGradientStops"/> contains more than one stop, the stops
    ///     are linearly interpolated to produce <see cref="DefaultPaletteSize"/> entries.
    ///   </item>
    ///   <item>
    ///     Otherwise, a palette is derived from <see cref="Theme.AccentColor"/> and
    ///     <see cref="Theme.SecondaryColor"/> via hue rotation.
    ///   </item>
    /// </list>
    /// </para>
    /// <para>This is a pure function of the <see cref="Theme"/> — no side effects.</para>
    /// </remarks>
    /// <param name="theme">Source theme.</param>
    /// <returns>
    /// A <see cref="IReadOnlyList{T}"/> of hex color strings suitable for node fills.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> ResolveEffectivePalette(Theme theme) =>
        ResolveEffectivePalette(theme, DefaultPaletteSize);

    /// <summary>
    /// Returns a chromatic palette sized for the caller's repeated-element needs.
    /// </summary>
    /// <param name="theme">Source theme.</param>
    /// <param name="desiredCount">Number of entries to produce for fallback palettes.</param>
    public static IReadOnlyList<string> ResolveEffectivePalette(Theme theme, int desiredCount)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (desiredCount < 1)
            throw new ArgumentOutOfRangeException(nameof(desiredCount), desiredCount, "desiredCount must be at least 1.");

        if (theme.NodePalette is { Count: > 0 } &&
            !ColorUtils.IsPaletteMonochrome(theme.NodePalette, theme.BackgroundColor))
            return theme.NodePalette;

        // Build fallback from gradient stops when they are available and meaningful.
        if (theme.UseBorderGradients && theme.BorderGradientStops is { Count: > 1 })
        {
            var gradientPalette = BuildPaletteFromGradientStops(theme.BorderGradientStops, desiredCount);
            if (!ColorUtils.IsPaletteMonochrome(gradientPalette, theme.BackgroundColor))
                return gradientPalette;
        }

        // Fall back to hue-rotation derivation from the theme's semantic colors.
        bool isLightBackground = ColorUtils.IsLight(theme.BackgroundColor);
        return BuildPaletteFromHueRotation(theme.AccentColor, theme.SecondaryColor, desiredCount, isLightBackground);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Samples <paramref name="count"/> evenly-distributed colors from the gradient
    /// by linearly interpolating between adjacent stop pairs.
    /// </summary>
    private static IReadOnlyList<string> BuildPaletteFromGradientStops(IReadOnlyList<string> stops, int count)
    {
        // Callers must supply at least two stops so there is an interpolatable range.
        if (stops.Count < 2)
            throw new ArgumentException("At least two gradient stops are required.", nameof(stops));

        var result = new List<string>(count);

        if (count == 1)
        {
            result.Add(stops[0]);
            return result;
        }

        for (int i = 0; i < count; i++)
        {
            double t = (double)i / (count - 1);
            double position = t * (stops.Count - 1);
            int lo = Math.Min((int)Math.Floor(position), stops.Count - 2);
            int hi = lo + 1;
            double blend = position - lo;
            result.Add(ColorUtils.Blend(stops[lo], stops[hi], blend));
        }

        return result;
    }

    /// <summary>
    /// Derives <paramref name="count"/> colors by rotating the hue of
    /// <paramref name="accentColor"/> and <paramref name="secondaryColor"/>
    /// in equal steps, alternating between the two base colors.
    /// </summary>
    private static IReadOnlyList<string> BuildPaletteFromHueRotation(
        string accentColor, string secondaryColor, int count, bool isLightBackground)
    {
        double hueStep = 360.0 / count;
        var result = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            string baseColor = i % 2 == 0 ? accentColor : secondaryColor;
            double rotation = Math.Floor(i / 2.0) * hueStep;
            result.Add(ColorUtils.RotateHue(baseColor, rotation, isLightBackground));
        }

        return result;
    }

    /// <summary>
    /// Builds an array of <paramref name="ringCount"/> visually distinct ring colors
    /// derived from the theme palette.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first entry is always <paramref name="outerColor"/>. Subsequent entries are
    /// chosen from a candidate pool built from theme semantic colors and
    /// <see cref="Theme.NodePalette"/> entries, maximizing hue distance from colors
    /// already selected and from <paramref name="centerColor"/>.
    /// </para>
    /// <para>
    /// When the candidate pool is exhausted, fallback colors are generated by
    /// rotating the last chosen color's hue by 55°.
    /// </para>
    /// </remarks>
    /// <param name="theme">Theme supplying semantic colors and optional palette.</param>
    /// <param name="ringCount">Number of ring colors to produce.</param>
    /// <param name="centerColor">Center node fill color — used for hue-distance contrast.</param>
    /// <param name="outerColor">Color for the outermost ring (index 0).</param>
    /// <param name="isLightBackground">
    /// Controls lightness clamping when generating fallback colors via hue rotation.
    /// </param>
    public static string[] BuildRingColors(Theme theme, int ringCount, string centerColor, string outerColor, bool isLightBackground)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (ringCount < 1)
            throw new ArgumentOutOfRangeException(nameof(ringCount), ringCount, "ringCount must be at least 1.");

        var chromaticPalette = ResolveEffectivePalette(theme, Math.Max(DefaultPaletteSize, ringCount * 2));

        var colors = new List<string>(ringCount)
        {
            outerColor,
        };

        if (ringCount == 1)
            return [.. colors];

        var candidatePool = new List<string>
        {
            ColorUtils.Vibrant(theme.SecondaryColor, chromaticPalette[0], 2.4),
            ColorUtils.Vibrant(theme.AccentColor, chromaticPalette[1 % chromaticPalette.Count], 2.4),
            ColorUtils.Vibrant(ColorUtils.Blend(theme.SecondaryColor, theme.AccentColor, 0.5), chromaticPalette[2 % chromaticPalette.Count], 2.6),
            ColorUtils.Vibrant(ColorUtils.Blend(theme.AccentColor, theme.PrimaryColor, 0.35), chromaticPalette[3 % chromaticPalette.Count], 2.5),
            ColorUtils.Vibrant(ColorUtils.Blend(theme.PrimaryColor, theme.SecondaryColor, 0.25), chromaticPalette[4 % chromaticPalette.Count], 2.5),
            ColorUtils.RotateHue(theme.SecondaryColor, 34, isLightBackground),
            ColorUtils.RotateHue(theme.AccentColor, -34, isLightBackground),
            ColorUtils.RotateHue(theme.PrimaryColor, 52, isLightBackground),
        };

        if (theme.NodePalette is { Count: > 0 })
        {
            for (int i = 0; i < theme.NodePalette.Count; i++)
            {
                string paletteColor = theme.NodePalette[i];
                string fallbackColor = chromaticPalette[i % chromaticPalette.Count];
                candidatePool.Add(ColorUtils.Vibrant(paletteColor, fallbackColor, 2.6));
                candidatePool.Add(ColorUtils.RotateHue(fallbackColor, 28, isLightBackground));
            }
        }

        candidatePool.AddRange(chromaticPalette);

        var distinctCandidates = candidatePool
            .Select(color => ColorUtils.Blend(color, theme.BackgroundColor, isLightBackground ? 0.06 : 0.10))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(color => ColorUtils.GetHueDistance(color, outerColor) >= 18)
            .ToList();

        if (ColorUtils.IsPaletteMonochrome(distinctCandidates, theme.BackgroundColor))
        {
            distinctCandidates = chromaticPalette
                .Select(color => ColorUtils.Blend(color, theme.BackgroundColor, isLightBackground ? 0.06 : 0.10))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(color => ColorUtils.GetHueDistance(color, outerColor) >= 18)
                .ToList();
        }

        while (colors.Count < ringCount)
        {
            string? nextColor = distinctCandidates
                .OrderByDescending(candidate => ColorUtils.GetMinimumHueDistance(candidate, colors.Append(centerColor)))
                .ThenByDescending(candidate => ColorUtils.GetContrastRatio(candidate, theme.BackgroundColor))
                .FirstOrDefault();

            if (nextColor is null)
            {
                nextColor = ColorUtils.RotateHue(colors[^1], 55, isLightBackground);
            }
            else
            {
                distinctCandidates.Remove(nextColor);
            }

            colors.Add(nextColor);
        }

        return [.. colors];
    }
}
