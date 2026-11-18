namespace WaveMP.Core.Scrobbling.Connections;

/// <summary>
/// Result of connecting a scrobbling service.
/// </summary>
public enum ScrobbleConnectStatus
{
    Connected,

    /// <summary>
    /// The service cannot be connected because WaveMP has no API account for it (Last.fm without
    /// <c>LASTFM_API_KEY</c> and <c>LASTFM_SHARED_SECRET</c>).
    /// </summary>
    NotConfigured,

    /// <summary>
    /// The service did not accept the token.
    /// </summary>
    InvalidToken,

    /// <summary>
    /// The service cannot be reached; the token was not checked.
    /// </summary>
    ServiceUnavailable,
}
