namespace WaveMP.Core.Lyrics.Options;

/// <summary>
/// Settings of the lyrics (configuration section <c>Lyrics</c>, e.g. <c>Lyrics__RequestInterval</c>). Whether lyrics
/// are downloaded at all is a library setting the admin switches in the app.
/// </summary>
public sealed class LyricsOptions
{
    public const string SectionName = "Lyrics";

    /// <summary>
    /// Address of the online lyrics database LRCLIB; no API key is needed.
    /// </summary>
    public Uri LrclibUrl { get; set; } = new("https://lrclib.net/");

    /// <summary>
    /// Shortest time between two requests to LRCLIB, so the download of a whole library does not flood the service.
    /// </summary>
    public TimeSpan RequestInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long a request to LRCLIB may take.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How often the download job tries a track when LRCLIB refuses requests (HTTP 429) or cannot be reached, before
    /// the run stops; the next run goes on with the track.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Wait of the download job before the next attempt, unless LRCLIB names one (Retry-After).
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Longest wait of the download job before the next attempt, also when LRCLIB asks for a longer one.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Largest lyrics file that is read, in bytes; larger files are skipped.
    /// </summary>
    public long MaxFileBytes { get; set; } = 1024 * 1024;
}
