namespace WaveMP.Core.OnlineMetadata.Options;

/// <summary>
/// Limits of the requests of the online lookup of artist pictures. The values come from the section
/// <c>ArtistPictures</c> of the configuration (set by <c>AddWaveMpArtistPictures</c>); without it, the defaults below
/// apply.
/// </summary>
public sealed class ArtistLookupClientOptions
{
    /// <summary>
    /// Largest answer of a service in bytes that is read (after decompression).
    /// </summary>
    public long MaxJsonBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// Largest picture in bytes that is downloaded (after decompression).
    /// </summary>
    public long MaxDownloadBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// How long a single request may take, including reading its answer.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
}
