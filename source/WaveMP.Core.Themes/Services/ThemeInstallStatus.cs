namespace WaveMP.Core.Themes.Services;

public enum ThemeInstallStatus
{
    /// <summary>
    /// The theme is installed.
    /// </summary>
    Installed,

    /// <summary>
    /// The installed theme is replaced.
    /// </summary>
    Replaced,

    /// <summary>
    /// The content is no ZIP file.
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// The package is rejected; the errors say why.
    /// </summary>
    Invalid,

    /// <summary>
    /// A theme with the id of the package is already installed.
    /// </summary>
    AlreadyInstalled,

    /// <summary>
    /// There is no installed theme with the id to replace.
    /// </summary>
    NotFound,

    /// <summary>
    /// The folder of the themes cannot be written.
    /// </summary>
    Unavailable,
}
