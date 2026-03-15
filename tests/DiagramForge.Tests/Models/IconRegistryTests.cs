using DiagramForge.Abstractions;
using DiagramForge.Models;

namespace DiagramForge.Tests.Models;

public class IconRegistryTests
{
    // ── RegisterPack ──────────────────────────────────────────────────────────

    [Fact]
    public void RegisterPack_AddsProvider()
    {
        var registry = new IconRegistry();
        registry.RegisterPack("test", new StubIconProvider("icon1"));

        Assert.Contains("test", registry.RegisteredPacks);
    }

    [Fact]
    public void RegisterPack_DuplicateName_Throws()
    {
        var registry = new IconRegistry();
        registry.RegisterPack("test", new StubIconProvider());

        Assert.Throws<ArgumentException>(() =>
            registry.RegisterPack("test", new StubIconProvider()));
    }

    [Fact]
    public void RegisterPack_WithAlias_RegistersBothNames()
    {
        var registry = new IconRegistry();
        registry.RegisterPack("full-name", "short", new StubIconProvider("icon1"));

        Assert.Contains("full-name", registry.RegisteredPacks);
        Assert.Contains("short", registry.RegisteredPacks);
    }

    [Fact]
    public void RegisterPack_IsCaseInsensitive()
    {
        var registry = new IconRegistry();
        registry.RegisterPack("Test", new StubIconProvider("icon1"));

        var result = registry.Resolve("test:icon1");
        Assert.NotNull(result);
    }

    // ── Resolve ───────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_NamespacedReference_ReturnsIcon()
    {
        var registry = new IconRegistry();
        registry.RegisterPack("heroicons", new StubIconProvider("shield-check"));

        var icon = registry.Resolve("heroicons:shield-check");

        Assert.NotNull(icon);
        Assert.Equal("shield-check", icon.Name);
    }

    [Fact]
    public void Resolve_BareName_SearchesAllPacks()
    {
        var registry = new IconRegistry();
        registry.RegisterPack("pack1", new StubIconProvider("cloud"));

        var icon = registry.Resolve("cloud");

        Assert.NotNull(icon);
        Assert.Equal("cloud", icon.Name);
    }

    [Fact]
    public void Resolve_UnknownIcon_ReturnsNull()
    {
        var registry = new IconRegistry();
        registry.RegisterPack("test", new StubIconProvider("known"));

        Assert.Null(registry.Resolve("unknown"));
    }

    [Fact]
    public void Resolve_UnknownPack_ReturnsNull()
    {
        var registry = new IconRegistry();
        registry.RegisterPack("test", new StubIconProvider("icon1"));

        Assert.Null(registry.Resolve("missing:icon1"));
    }

    [Fact]
    public void Resolve_NullOrEmpty_ReturnsNull()
    {
        var registry = new IconRegistry();

        Assert.Null(registry.Resolve(null!));
        Assert.Null(registry.Resolve(""));
        Assert.Null(registry.Resolve("  "));
    }

    [Fact]
    public void Resolve_ViaAlias_ReturnsIcon()
    {
        var registry = new IconRegistry();
        registry.RegisterPack("full-name", "short", new StubIconProvider("icon1"));

        var icon = registry.Resolve("short:icon1");

        Assert.NotNull(icon);
        Assert.Equal("icon1", icon.Name);
    }

    // ── Stub ──────────────────────────────────────────────────────────────────

    private sealed class StubIconProvider(params string[] iconNames) : IIconProvider
    {
        public DiagramIcon? GetIcon(string name) =>
            iconNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                ? new DiagramIcon("stub", name, "0 0 24 24", "<path d=\"M0 0\"/>")
                : null;

        public IEnumerable<string> AvailableIcons => iconNames;
    }
}
