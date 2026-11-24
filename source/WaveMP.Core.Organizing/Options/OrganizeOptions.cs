using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Organizing.Options;

/// <summary>
/// Settings of the organization of the media files (configuration section <c>Organizing</c>, e.g.
/// <c>Organizing__PreviewLifetime</c>). The uploaded covers of the own albums lie in the folder
/// <c>own-album-covers</c> of the data folder (see <see cref="DataPaths"/>).
/// </summary>
public sealed class OrganizeOptions
{
    public const string SectionName = "Organizing";
    public const string CoverFolderName = "own-album-covers";

    /// <summary>
    /// Absolute path of the folder of the own album covers, set from the data folder.
    /// </summary>
    public string CoverPath { get; set; } = DataPaths.DefaultDataPath + "/" + CoverFolderName;

    /// <summary>
    /// How long a preview can be applied.
    /// </summary>
    public TimeSpan PreviewLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How long previews that were never applied are kept.
    /// </summary>
    public TimeSpan PreviewRetention { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// How many songs a library preview plans at a time.
    /// </summary>
    public int PlanningBatchSize { get; set; } = 500;

    /// <summary>
    /// After how many moved songs the run updates the library.
    /// </summary>
    public int RefreshBatchSize { get; set; } = 200;

    /// <summary>
    /// Largest uploaded cover of an own album in bytes.
    /// </summary>
    public long MaxCoverBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Largest number of songs of an own album.
    /// </summary>
    public int MaxAlbumTracks { get; set; } = 1000;
}
