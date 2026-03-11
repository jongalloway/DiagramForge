using System.Text.Json.Serialization;

namespace DiagramForge.Models;

/// <summary>
/// Source-generated JSON serialization context for <see cref="Theme"/>.
/// </summary>
[JsonSerializable(typeof(Theme))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
internal partial class ThemeJsonContext : JsonSerializerContext
{
}
