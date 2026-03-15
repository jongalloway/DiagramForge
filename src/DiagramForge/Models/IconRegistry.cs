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
    /// Both names are validated before either is registered to ensure an atomic operation.
    /// </summary>
    public void RegisterPack(string packName, string alias, IIconProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packName);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentNullException.ThrowIfNull(provider);

        // Validate both names up-front before touching the registry to keep the
        // operation atomic — partial registration is not observable to callers.
        if (_packs.ContainsKey(packName))
            throw new ArgumentException($"An icon pack named '{packName}' is already registered.", nameof(packName));
        if (_packs.ContainsKey(alias))
            throw new ArgumentException($"An icon pack named '{alias}' is already registered.", nameof(alias));
        if (string.Equals(packName, alias, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Pack name and alias must be different.", nameof(alias));

        // TryAdd may still fail under concurrent load, but the existing single-name
        // overload already throws in that case, so the behaviour is consistent.
        if (!_packs.TryAdd(packName, provider))
            throw new ArgumentException($"An icon pack named '{packName}' is already registered.", nameof(packName));

        if (!_packs.TryAdd(alias, provider))
        {
            _packs.TryRemove(packName, out _);
            throw new ArgumentException($"An icon pack named '{alias}' is already registered.", nameof(alias));
        }
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

        // Bare name — search all packs in sorted name order for deterministic results.
        foreach (var packName in _packs.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var icon = _packs[packName].GetIcon(reference);
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
