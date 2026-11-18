using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Packages;

/// <summary>
/// The unpacked files of a theme package, by their path in the package (see <see cref="PackagePaths"/>), without
/// folders, ignored system files and documents. If <see cref="Errors"/> is not empty, the package is rejected and
/// <see cref="Files"/> may be incomplete.
/// </summary>
internal sealed class ThemePackage
{
    public required IReadOnlyDictionary<string, byte[]> Files { get; init; }

    public required IReadOnlyList<ThemeError> Errors { get; init; }
}
