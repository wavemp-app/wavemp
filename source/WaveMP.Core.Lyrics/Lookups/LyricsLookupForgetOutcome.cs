namespace WaveMP.Core.Lyrics.Lookups;

/// <summary>
/// What forgetting the lookup of a track did.
/// </summary>
public enum LyricsLookupForgetOutcome
{
    /// <summary>
    /// The lookup found no lyrics and is forgotten; the next run of the download job looks the track up again.
    /// </summary>
    Forgotten,

    /// <summary>
    /// The track has no stored lookup result (never looked up, or already forgotten).
    /// </summary>
    NotLookedUp,

    /// <summary>
    /// The lookup found lyrics or the track already had lyrics; such results are kept.
    /// </summary>
    FoundLyrics,

    /// <summary>
    /// The track does not exist.
    /// </summary>
    TrackNotFound,
}
