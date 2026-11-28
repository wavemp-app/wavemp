namespace WaveMP.Core.Analysis.Services;

/// <summary>
/// What the analysis of a track did.
/// </summary>
/// <param name="Outcome">Whether the track was analyzed.</param>
/// <param name="NeedsReview">Whether a file needs a look of the admin (an unsure segment or a mismatch).</param>
/// <param name="Failed">Whether the sound of a file could not be read.</param>
/// <param name="LyricsTransient">Whether LRCLIB could not be asked; the lyrics job looks the track up later.</param>
public sealed record SongAnalysisResult(
    SongAnalysisOutcome Outcome,
    bool NeedsReview = false,
    bool Failed = false,
    bool LyricsTransient = false);

/// <summary>
/// Whether a track was analyzed.
/// </summary>
public enum SongAnalysisOutcome
{
    Analyzed,

    /// <summary>
    /// The track has neither an audio file nor a video.
    /// </summary>
    NoFiles,

    TrackNotFound,
}
