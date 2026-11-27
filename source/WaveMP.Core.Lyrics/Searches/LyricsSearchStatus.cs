namespace WaveMP.Core.Lyrics.Searches;

/// <summary>
/// Whether a search for lyrics ran.
/// </summary>
public enum LyricsSearchStatus
{
    /// <summary>
    /// LRCLIB answered; the matches may be empty.
    /// </summary>
    Searched,

    /// <summary>
    /// The download of lyrics is switched off in the library settings.
    /// </summary>
    Disabled,

    /// <summary>
    /// The track does not exist.
    /// </summary>
    TrackNotFound,

    /// <summary>
    /// LRCLIB refuses requests for a while.
    /// </summary>
    RateLimited,

    /// <summary>
    /// LRCLIB cannot be reached.
    /// </summary>
    Unavailable,
}
