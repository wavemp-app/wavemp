using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.ArtistPictures.Options;

/// <summary>
/// Settings of the artist pictures (configuration section <c>ArtistPictures</c>, e.g.
/// <c>ArtistPictures__MaxUploadBytes</c>). Uploaded pictures and pictures taken over from the online lookup lie in the
/// folder <c>artist-pictures</c> of the data folder (see <see cref="DataPaths"/>).
/// </summary>
public sealed class ArtistPictureOptions
{
    public const string SectionName = "ArtistPictures";
    public const string FolderName = "artist-pictures";

    /// <summary>
    /// Absolute path of the folder of the stored artist pictures, set from the data folder.
    /// </summary>
    public string RootPath { get; set; } = DataPaths.DefaultDataPath + "/" + FolderName;

    /// <summary>
    /// Largest uploaded picture in bytes.
    /// </summary>
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Largest picture in bytes that the online lookup downloads.
    /// </summary>
    public long MaxDownloadBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// Largest answer in bytes of an online service that the online lookup reads.
    /// </summary>
    public long MaxJsonBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// How long the answers of the online services are reused before they are asked again.
    /// </summary>
    public TimeSpan LookupCacheDuration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// How long a single request of the online lookup may take.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
}
