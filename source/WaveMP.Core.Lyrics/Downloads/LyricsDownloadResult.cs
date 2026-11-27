namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// The result of a download.
/// </summary>
/// <param name="Outcome">What the download did.</param>
/// <param name="RetryAfter">How long LRCLIB asks to wait, for <see cref="LyricsDownloadOutcome.RateLimited"/>.</param>
public sealed record LyricsDownloadResult(LyricsDownloadOutcome Outcome, TimeSpan? RetryAfter = null)
{
    /// <summary>
    /// Whether the saved lyrics belong to another version of the song, so only their plain lines were kept.
    /// </summary>
    public bool OtherVersion { get; init; }

    /// <summary>
    /// Whether lyrics were saved.
    /// </summary>
    public bool IsSaved => Outcome is LyricsDownloadOutcome.SavedToFile or LyricsDownloadOutcome.SavedToDatabase;

    /// <summary>
    /// Whether LRCLIB could not answer; another attempt later may succeed.
    /// </summary>
    public bool IsTransient => Outcome is LyricsDownloadOutcome.RateLimited or LyricsDownloadOutcome.Unavailable;
}
