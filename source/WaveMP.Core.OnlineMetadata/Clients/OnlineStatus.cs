namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The answer of MusicBrainz or the Cover Art Archive to a request.
/// </summary>
public enum OnlineStatus
{
    /// <summary>
    /// The service answered with content.
    /// </summary>
    Found,

    /// <summary>
    /// The service knows nothing for the request (HTTP 404), e.g. a release without cover.
    /// </summary>
    NotFound,

    /// <summary>
    /// The service cannot be reached (no internet connection, timeout), failed, kept refusing requests (HTTP 503) or
    /// answered something that cannot be read.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The online search is switched off; no request was sent.
    /// </summary>
    Disabled,
}
