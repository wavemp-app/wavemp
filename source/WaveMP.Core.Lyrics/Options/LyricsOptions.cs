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
    /// How long the download job waits before it looks up a track again whose lookup found no lyrics (LRCLIB had none
    /// or listed the track as instrumental), because LRCLIB grows over time. Tracks that already had lyrics in their
    /// tags are checked again after the same wait (and at once when a scan read their files after the check); LRCLIB
    /// is asked only if the tags lost their lyrics. Zero or less never looks such a track up again after a wait; an
    /// admin can still download its lyrics at any time.
    /// </summary>
    public TimeSpan LookUpAgainAfter { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Largest lyrics file that is read, in bytes; larger files are skipped. Uploaded lyrics may not be longer.
    /// </summary>
    public long MaxFileBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Longest time to solve the challenge LRCLIB asks for before it takes published lyrics (a proof of work that
    /// usually takes a few seconds); publishing fails after it.
    /// </summary>
    public TimeSpan PublishChallengeTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
