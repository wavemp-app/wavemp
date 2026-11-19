using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Themes.Options;

/// <summary>
/// Settings of the uploaded themes (configuration section <c>Themes</c>, e.g. <c>Themes__MaxUploadBytes</c>). The
/// installed themes lie in the folder <c>themes</c> of the data folder (see <see cref="DataPaths"/>). The limits keep
/// a package from filling the memory or the disk (ZIP bombs): they are checked while the package is unpacked, not
/// taken from the sizes the ZIP file claims.
/// </summary>
public sealed class ThemeOptions
{
    public const string SectionName = "Themes";
    public const string FolderName = "themes";

    /// <summary>
    /// Absolute path of the folder of the installed themes, set from the data folder.
    /// </summary>
    public string RootPath { get; set; } = DataPaths.DefaultDataPath + "/" + FolderName;

    /// <summary>
    /// Largest uploaded ZIP file in bytes (10 MiB).
    /// </summary>
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Largest sum of the unpacked files of a package in bytes (32 MiB).
    /// </summary>
    public long MaxUnpackedBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>
    /// Largest unpacked file of a package in bytes (5 MiB); the CSS file and the fonts have their own limits.
    /// </summary>
    public long MaxFileBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Largest CSS file in bytes (1 MiB).
    /// </summary>
    public long MaxStylesheetBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Largest font file (WOFF2) of a package in bytes (2 MiB).
    /// </summary>
    public long MaxFontBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// Largest number of entries (files and folders) of a package.
    /// </summary>
    public int MaxEntries { get; set; } = 256;
}
