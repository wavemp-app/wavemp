namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// The answer of LRCLIB to published lyrics.
/// </summary>
public enum LrclibPublishStatus
{
    /// <summary>
    /// LRCLIB took the lyrics.
    /// </summary>
    Published,

    /// <summary>
    /// LRCLIB refused the lyrics, e.g. because a field is missing or the solved challenge was not accepted.
    /// </summary>
    Rejected,

    /// <summary>
    /// LRCLIB refuses requests for a while (HTTP 429).
    /// </summary>
    RateLimited,

    /// <summary>
    /// LRCLIB cannot be reached, failed, or the challenge could not be solved in time.
    /// </summary>
    Unavailable,
}
