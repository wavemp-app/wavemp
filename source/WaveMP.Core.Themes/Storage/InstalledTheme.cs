using WaveMP.Core.Themes.Definitions;

namespace WaveMP.Core.Themes.Storage;

/// <summary>
/// An uploaded theme as it is installed in the folder of the themes (<c>/data/themes/{id}</c>, described by its
/// <c>theme.json</c>).
/// </summary>
public sealed record InstalledTheme
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Author { get; init; }

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
    /// Name of the CSS file (see <see cref="ThemeFiles"/>).
    /// </summary>
    public required string Stylesheet { get; init; }

    /// <summary>
    /// Name of the preview picture of each variant.
    /// </summary>
    public required IReadOnlyDictionary<ThemeVariant, string> Previews { get; init; }

    /// <summary>
    /// Names of all files of the theme.
    /// </summary>
    public required IReadOnlyList<string> Files { get; init; }

    /// <summary>
    /// Size of all files of the theme in bytes.
    /// </summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// When this version of the theme was installed.
    /// </summary>
    public required DateTimeOffset InstalledAt { get; init; }
}
