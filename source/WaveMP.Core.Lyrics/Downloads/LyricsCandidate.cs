namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Synchronized lyrics of LRCLIB for a track that are looked up without relying on its duration (see
/// <see cref="ILyricsDownloader.FindCandidateAsync"/>): the song analysis finds with their times where the song lies
/// in a file with an intro, before they are saved.
/// </summary>
/// <param name="Outcome">What the lookup found: <see cref="LyricsDownloadOutcome.SavedToFile"/> is never used here;
/// <see cref="LyricsDownloadOutcome.NotNeeded"/> stands for a found candidate.</param>
/// <param name="Record">The track of LRCLIB with synchronized lyrics; <c>null</c> without one.</param>
/// <param name="RetryAfter">How long LRCLIB asks to wait, for <see cref="LyricsDownloadOutcome.RateLimited"/>.</param>
public sealed record LyricsCandidate(
    LyricsDownloadOutcome Outcome,
    LrclibRecord? Record = null,
    TimeSpan? RetryAfter = null)
{
    /// <summary>
    /// Whether LRCLIB refused the request or could not be reached; the track is looked up again later.
    /// </summary>
    public bool IsTransient => Outcome is LyricsDownloadOutcome.RateLimited or LyricsDownloadOutcome.Unavailable;

    /// <summary>
    /// The duration of the recording the lyrics belong to, if LRCLIB knows it.
    /// </summary>
    public TimeSpan? Duration => Record?.Duration is { } seconds && seconds > 0
        ? TimeSpan.FromSeconds(seconds)
        : null;
}
