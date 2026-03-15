using System.Collections.Concurrent;
using DiagramForge.Abstractions;

namespace DiagramForge.Models;

/// <summary>
/// Aggregates named icon packs and resolves icon references in the form
/// <c>pack:icon-name</c> or bare <c>icon-name</c> (searched across all packs).
/// Thread-safe: packs may be registered and resolved concurrently.
/// </summary>
public sealed class IconRegistry
{
    private readonly ConcurrentDictionary<string, IIconProvider> _packs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers an icon provider under the given <paramref name="packName"/>.
    /// </summary>
    /// <exception cref="ArgumentException">A pack with the same name is already registered.</exception>
    public void RegisterPack(string packName, IIconProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packName);
        ArgumentNullException.ThrowIfNull(provider);

        if (!_packs.TryAdd(packName, provider))
            throw new ArgumentException($"An icon pack named '{packName}' is already registered.", nameof(packName));
    }

    /// <summary>
    /// Registers an icon provider under both <paramref name="packName"/> and <paramref name="alias"/>.
    /// </summary>
    public void RegisterPack(string packName, string alias, IIconProvider provider)
    {
        RegisterPack(packName, provider);

        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        if (!_packs.TryAdd(alias, provider))
            throw new ArgumentException($"An icon pack named '{alias}' is already registered.", nameof(alias));
    }

    /// <summary>
    /// Resolves an icon reference. Supports:
    /// <list type="bullet">
    ///   <item><c>pack:icon-name</c> — looks up the icon in the named pack.</item>
    ///   <item><c>icon-name</c> — searches all registered packs, returning the first match.</item>
    /// </list>
    /// Returns <see langword="null"/> if no match is found.
    /// </summary>
    public DiagramIcon? Resolve(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        int colonIndex = reference.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex > 0 && colonIndex < reference.Length - 1)
        {
            string packName = reference[..colonIndex];
            string iconName = reference[(colonIndex + 1)..];

            if (_packs.TryGetValue(packName, out var provider))
                return provider.GetIcon(iconName);

            return null;
        }

        // Bare name — search all packs.
        foreach (var provider in _packs.Values)
        {
            var icon = provider.GetIcon(reference);
            if (icon is not null)
                return icon;
        }

        return null;
    }

    /// <summary>
    /// Returns all registered pack names.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredPacks => [.. _packs.Keys];
}
