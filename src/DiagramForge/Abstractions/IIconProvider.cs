using DiagramForge.Models;

namespace DiagramForge.Abstractions;

/// <summary>
/// Provides icons by name within a single icon pack.
/// </summary>
public interface IIconProvider
{
    /// <summary>
    /// Resolves an icon by <paramref name="name"/> within this provider's pack.
    /// Returns <see langword="null"/> if the icon is not found.
    /// </summary>
    DiagramIcon? GetIcon(string name);

    /// <summary>
    /// Returns all icon names available in this provider.
    /// </summary>
    IEnumerable<string> AvailableIcons { get; }
}
