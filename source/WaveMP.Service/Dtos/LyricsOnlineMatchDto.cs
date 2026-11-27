namespace WaveMP.Service.Dtos;

/// <summary>
/// A track of the online lyrics service (LRCLIB) with lyrics.
/// </summary>
public sealed class LyricsOnlineMatchDto
{
    /// <summary>
    /// Id of the track at LRCLIB; choose it with <c>PUT /api/v1/tracks/{id}/lyrics/online-match</c>.
    /// </summary>
    public required long Id { get; init; }

    public required string Title { get; init; }

    public string? Artist { get; init; }

    public string? Album { get; init; }

    /// <summary>
    /// Duration in seconds, if known.
    /// </summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// Whether the track has synchronized lyrics.
    /// </summary>
    public required bool Synchronized { get; init; }

    /// <summary>
    /// Whether its duration fits the track (at most 3 seconds apart, or one of them unknown). Lyrics of a track whose
    /// duration does not fit are kept without their times, as lyrics of another version.
    /// </summary>
    public required bool FitsDuration { get; init; }
}
