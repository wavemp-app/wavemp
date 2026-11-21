namespace WaveMP.Service.Dtos;

/// <summary>
/// The stored lookups of lyrics that found no lyrics, over all tracks.
/// </summary>
public sealed class LyricsLookupSummaryDto
{
    /// <summary>
    /// Tracks the online lyrics service had no lyrics for.
    /// </summary>
    public required int NotFoundCount { get; init; }

    /// <summary>
    /// Tracks the online lyrics service lists as instrumental.
    /// </summary>
    public required int InstrumentalCount { get; init; }

    /// <summary>
    /// How long the download job waits before it looks up such a track again, in minutes (configuration
    /// <c>Lyrics__LookUpAgainAfter</c>); <c>null</c> if it never does.
    /// </summary>
    public int? LookUpAgainAfterMinutes { get; init; }
}
