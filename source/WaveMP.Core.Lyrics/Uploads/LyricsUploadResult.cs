namespace WaveMP.Core.Lyrics.Uploads;

/// <summary>
/// The result of an upload.
/// </summary>
/// <param name="Outcome">What saving did.</param>
/// <param name="Publication">What publishing to LRCLIB did.</param>
public sealed record LyricsUploadResult(
    LyricsUploadOutcome Outcome,
    LyricsPublication Publication = LyricsPublication.NotRequested)
{
    /// <summary>
    /// Whether the lyrics were saved.
    /// </summary>
    public bool IsSaved => Outcome is LyricsUploadOutcome.SavedToFile or LyricsUploadOutcome.SavedToDatabase;
}
