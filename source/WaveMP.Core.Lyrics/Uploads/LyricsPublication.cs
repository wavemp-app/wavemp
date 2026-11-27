namespace WaveMP.Core.Lyrics.Uploads;

/// <summary>
/// What publishing uploaded lyrics to LRCLIB did.
/// </summary>
public enum LyricsPublication
{
    /// <summary>
    /// Publishing was not asked for, or the lyrics were not saved.
    /// </summary>
    NotRequested,

    /// <summary>
    /// LRCLIB took the lyrics.
    /// </summary>
    Published,

    /// <summary>
    /// The download of lyrics is switched off in the library settings, which also keeps WaveMP from contacting
    /// LRCLIB to publish.
    /// </summary>
    Disabled,

    /// <summary>
    /// The track lacks what LRCLIB needs: title, artist, album and duration.
    /// </summary>
    MissingMetadata,

    /// <summary>
    /// LRCLIB refused the lyrics.
    /// </summary>
    Rejected,

    /// <summary>
    /// LRCLIB refuses requests for a while.
    /// </summary>
    RateLimited,

    /// <summary>
    /// LRCLIB cannot be reached, or its challenge could not be solved in time.
    /// </summary>
    Unavailable,
}
