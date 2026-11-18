namespace WaveMP.Core.Scrobbling.Plays;

/// <summary>
/// Result of reporting "Now Playing".
/// </summary>
public enum NowPlayingStatus
{
    /// <summary>
    /// At least one service took the report.
    /// </summary>
    Reported,

    /// <summary>
    /// Nothing was reported: no service is connected, none took the report, or the song is not reported by the rules
    /// (shorter than 30 seconds, no artist).
    /// </summary>
    NotReported,

    /// <summary>
    /// The track does not exist.
    /// </summary>
    TrackNotFound,
}
