namespace WaveMP.Service.Dtos;

/// <summary>
/// The state of a scrobbling service for the current profile. The credentials are never returned.
/// </summary>
public sealed class ScrobblingServiceDto
{
    public required ScrobbleServiceDto Service { get; init; }

    /// <summary>
    /// True if the service can be connected. Last.fm needs the API account of the server (environment variables
    /// LASTFM_API_KEY and LASTFM_SHARED_SECRET); ListenBrainz is always available.
    /// </summary>
    public required bool IsAvailable { get; init; }

    public required ScrobbleConnectionStatusDto Status { get; init; }

    /// <summary>
    /// Name of the account at the service; null if not connected.
    /// </summary>
    public string? AccountName { get; init; }

    /// <summary>
    /// When the service was connected; null if not connected.
    /// </summary>
    public DateTimeOffset? ConnectedAt { get; init; }

    /// <summary>
    /// When the service accepted scrobbles the last time; null before the first.
    /// </summary>
    public DateTimeOffset? LastScrobbledAt { get; init; }

    /// <summary>
    /// Scrobbles that wait to be sent, e.g. while the service cannot be reached.
    /// </summary>
    public required int QueuedScrobbles { get; init; }

    /// <summary>
    /// Last.fm only, while available: the page of the web authentication with the API key of the server. The client
    /// adds its callback URL as parameter <c>cb</c> and opens it; null otherwise.
    /// </summary>
    public Uri? AuthorizationUrl { get; init; }
}
