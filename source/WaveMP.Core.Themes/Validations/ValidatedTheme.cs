using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Manifests;

namespace WaveMP.Core.Themes.Validations;

/// <summary>
/// A checked theme package, ready to be installed: its manifest and its files under their installed names.
/// </summary>
public sealed class ValidatedTheme
{
    public required ThemeManifest Manifest { get; init; }

    /// <summary>
    /// The CSS file with the addresses of the installed files.
    /// </summary>
    public required ValidatedThemeFile Stylesheet { get; init; }

    /// <summary>
    /// All files to install, the CSS file first; files with the same content appear once.
    /// </summary>
    public required IReadOnlyList<ValidatedThemeFile> Files { get; init; }

    /// <summary>
    /// The installed name of the preview picture of each variant.
    /// </summary>
    public required IReadOnlyDictionary<ThemeVariant, string> Previews { get; init; }
}
