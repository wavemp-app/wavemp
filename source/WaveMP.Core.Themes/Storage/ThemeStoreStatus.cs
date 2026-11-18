namespace WaveMP.Core.Themes.Storage;

public enum ThemeStoreStatus
{
    /// <summary>
    /// The theme is installed (or replaced).
    /// </summary>
    Saved,

    /// <summary>
    /// A theme with the id is already installed.
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
