namespace WaveMP.Core.Lyrics.Jobs;

/// <summary>
/// Result of a run of the download job, stored as JSON with the run.
/// </summary>
/// <param name="TracksChecked">Tracks looked up (or skipped because they have lyrics).</param>
/// <param name="Downloaded">Tracks whose lyrics were saved, as file or in the database.</param>
/// <param name="NotFound">Tracks LRCLIB has no lyrics for, also instrumental ones.</param>
/// <param name="Skipped">Tracks that already had lyrics.</param>
/// <param name="Stopped">The run stopped early because the download was switched off.</param>
public sealed record LyricsDownloadJobResult(
    int TracksChecked,
    int Downloaded,
    int NotFound,
    int Skipped,
    bool Stopped);
