namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// The answer of LRCLIB to a request.
/// </summary>
public enum LrclibStatus
{
    /// <summary>
    /// LRCLIB returned at least one track.
    /// </summary>
    Found,

    /// <summary>
    /// LRCLIB knows no matching track.
    /// </summary>
    NotFound,

    /// <summary>
    /// LRCLIB refuses requests for a while (HTTP 429).
    /// </summary>
    RateLimited,

    /// <summary>
    /// LRCLIB cannot be reached (no internet connection, timeout) or failed.
    /// </summary>
    Unavailable,
}
