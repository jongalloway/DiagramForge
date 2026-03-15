using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Tests;

public class DiagramRendererIconTests
{
    // ── Built-in icons resolve for architecture diagrams ──────────────────────

    [Theory]
    [InlineData("cloud")]
    [InlineData("database")]
    [InlineData("disk")]
    [InlineData("internet")]
    [InlineData("server")]
    public void Render_ArchitectureDiagram_ResolvesBuiltInIcon(string icon)
    {
        var renderer = new DiagramRenderer();
        string svg = renderer.Render($"architecture-beta\n  service svc({icon})[Label]");

        // The SVG should contain an icon <svg> element with the 48px icon size.
        Assert.Contains("width=\"48.00\"", svg);
    }

    [Fact]
    public void Render_ArchitectureDiagram_IconSvgContentPresent()
    {
        var renderer = new DiagramRenderer();
        string svg = renderer.Render("architecture-beta\n  service svc(database)[Database]");

        // The built-in database icon contains an ellipse element.
        Assert.Contains("<ellipse", svg);
    }

    // ── RegisterIconPack ─────────────────────────────────────────────────────

    [Fact]
    public void RegisterIconPack_ReturnsSameRenderer()
    {
        var renderer = new DiagramRenderer();
        var result = renderer.RegisterIconPack("test", new StubIconProvider());

        Assert.Same(renderer, result);
    }

    [Fact]
    public void RegisterIconPack_IconResolvesInRender()
    {
        var renderer = new DiagramRenderer();
        renderer.RegisterIconPack("custom", new StubIconProvider("myicon"));

        // Architecture diagram referencing "custom:myicon" — parser captures the
        // pack:name token verbatim, so the registry resolves it during rendering.
        string svg = renderer.Render("architecture-beta\n  service svc(custom:myicon)[Label]");

        // The stub icon's path content must appear in the rendered SVG.
        Assert.Contains("d=\"M0 0\"", svg);
    }

    [Fact]
    public void IconRegistry_BuiltinPack_RegisteredByDefault()
    {
        var renderer = new DiagramRenderer();

        Assert.Contains("builtin", renderer.IconRegistry.RegisteredPacks);
        Assert.NotNull(renderer.IconRegistry.Resolve("builtin:cloud"));
    }

    // ── Stub ──────────────────────────────────────────────────────────────────

    private sealed class StubIconProvider(params string[] iconNames) : IIconProvider
    {
        public DiagramIcon? GetIcon(string name) =>
            iconNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                ? new DiagramIcon("custom", name, "0 0 24 24", """<path d="M0 0"/>""")
                : null;

        public IEnumerable<string> AvailableIcons => iconNames;
    }
}
