using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Recaps.Options;

/// <summary>
/// Settings of the statistics and recaps (configuration section <c>Recaps</c>, e.g. <c>Recaps__MaxVideoBytes</c>). The
/// videos of recaps are converted in the folder <c>temp/recap-videos</c> of the data folder (see
/// <see cref="DataPaths"/>), which is cleaned up automatically.
/// </summary>
public sealed class RecapOptions
{
    public const string SectionName = "Recaps";

    /// <summary>
    /// Folder of the temporary files in the data folder.
    /// </summary>
    public const string FolderName = "temp";

    /// <summary>
    /// Folder of the videos in <see cref="FolderName"/>.
    /// </summary>
    public const string VideoFolderName = "recap-videos";

    /// <summary>
    /// Absolute path of the folder the videos are converted in, set from the data folder.
    /// </summary>
    public string VideoPath { get; set; } = DataPaths.DefaultDataPath + "/" + FolderName + "/" + VideoFolderName;

    /// <summary>
    /// Largest uploaded video in bytes.
    /// </summary>
    public long MaxVideoBytes { get; set; } = 200 * 1024 * 1024;

    /// <summary>
    /// How long the conversion of a video may take.
    /// </summary>
    public TimeSpan ConversionTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Files in the folder of the videos older than this are left over (e.g. from a restart during a conversion) and
    /// are deleted.
    /// </summary>
    public TimeSpan TemporaryFileMaxAge { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How often the folder of the videos is cleaned up.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(15);
}
