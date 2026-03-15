namespace DiagramForge.Icons.Heroicons;

/// <summary>
/// Extension methods for registering the Heroicons icon pack with DiagramForge.
/// </summary>
public static class HeroiconsExtensions
{
    /// <summary>
    /// Registers the Heroicons (outline, 24×24) icon pack with the renderer
    /// under the pack name <c>"heroicons"</c>.
    /// </summary>
    /// <returns>The renderer, for fluent chaining.</returns>
    public static DiagramRenderer UseHeroicons(this DiagramRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.RegisterIconPack("heroicons", new HeroiconsProvider());
        return renderer;
    }
}
