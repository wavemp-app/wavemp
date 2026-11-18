using WaveMP.Core.Themes.Definitions;

namespace WaveMP.Core.Themes.Manifests;

/// <summary>
/// The checked content of <c>manifest.json</c> of a theme package.
/// </summary>
public sealed class ThemeManifest
{
    /// <summary>
    /// Id of the theme (see <see cref="ThemeIdRules"/>).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Name of the theme, shown as it is in every language.
    /// </summary>
    public required string Name { get; init; }

    public required string Author { get; init; }

    /// <summary>
    /// Version of the theme itself, e.g. <c>1.0.0</c>.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// The version of the theme API the theme is written for.
    /// </summary>
    public required int ThemeApi { get; init; }

    /// <summary>
    /// The variants of the theme, the first one is shown where the appearance of a profile has no say.
    /// </summary>
    public required IReadOnlyList<ThemeVariant> Variants { get; init; }

    /// <summary>
    /// Path of the preview picture of each variant in the package.
    /// </summary>
    public required IReadOnlyDictionary<ThemeVariant, string> Previews { get; init; }
}
