namespace WaveMP.Core.Themes.Packages;

/// <summary>
/// What a file of a theme package is, by its path.
/// </summary>
internal enum PackageFileKind
{
    /// <summary>
    /// <c>manifest.json</c> at the root of the package.
    /// </summary>
    Manifest,

    /// <summary>
    /// The CSS file of the theme.
    /// </summary>
    Stylesheet,

    Svg,

    Png,

    WebP,

    /// <summary>
    /// A text about the theme, e.g. its license or read-me; allowed, but not installed.
    /// </summary>
    Document,

    /// <summary>
    /// Any other file; such a package is rejected.
    /// </summary>
    Unsupported,
}
