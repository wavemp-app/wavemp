namespace WaveMP.Core.Lyrics.Lookups;

/// <summary>
/// Whether a track has a stored lookup result.
/// </summary>
public enum LyricsLookupStatus
{
    /// <summary>
    /// The track has a stored lookup result.
    /// </summary>
    Found,

    /// <summary>
    /// The track was never looked up, or its result was forgotten.
    /// </summary>
    NotLookedUp,

    /// <summary>
    /// The track does not exist.
    /// </summary>
    TrackNotFound,
}
