namespace WaveMP.Core.Scrobbling.Options;

/// <summary>
/// Settings of the scrobbling (configuration section <c>Scrobbling</c>, e.g. <c>Scrobbling__RetryDelay</c>). The
/// credentials of the Last.fm API account come from the environment variables <c>LASTFM_API_KEY</c> and
/// <c>LASTFM_SHARED_SECRET</c>; without them, Last.fm cannot be connected. ListenBrainz needs no configuration.
/// </summary>
public sealed class ScrobblingOptions
{
    public const string SectionName = "Scrobbling";

    /// <summary>
    /// Environment variable with the API key of the Last.fm API account.
    /// </summary>
    public const string LastFmApiKeyVariable = "LASTFM_API_KEY";

    /// <summary>
    /// Environment variable with the shared secret of the Last.fm API account.
    /// </summary>
    public const string LastFmSharedSecretVariable = "LASTFM_SHARED_SECRET";

    /// <summary>
    /// API key of the Last.fm API account; <c>null</c> if not set.
    /// </summary>
    public string? LastFmApiKey { get; set; }

    /// <summary>
    /// Shared secret of the Last.fm API account, which signs the requests; <c>null</c> if not set.
    /// </summary>
    public string? LastFmSharedSecret { get; set; }

    /// <summary>
    /// Address of the Last.fm API.
    /// </summary>
    public Uri LastFmApiUrl { get; set; } = new("https://ws.audioscrobbler.com/2.0/");

    /// <summary>
    /// Page of Last.fm where users allow WaveMP to scrobble (web authentication).
    /// </summary>
    public Uri LastFmAuthorizationUrl { get; set; } = new("https://www.last.fm/api/auth/");

    /// <summary>
    /// Address of the ListenBrainz API, e.g. of a server of one's own.
    /// </summary>
    public Uri ListenBrainzApiUrl { get; set; } = new("https://api.listenbrainz.org/");

    /// <summary>
    /// How long a request to a service may take.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Most scrobbles sent in one request; Last.fm accepts at most 50.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Wait before the first new attempt when a service cannot be reached; it doubles with every further failed
    /// attempt up to <see cref="MaxRetryDelay"/>.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Longest wait between two attempts.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Scrobbles older than this are dropped from the queue: Last.fm does not accept plays older than 14 days.
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// Wait after the start before the queue left from the last run is sent.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Longest time the queue rests without a new play; it is checked again after this time at the latest.
    /// </summary>
    public TimeSpan IdleInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Whether the Last.fm API account is configured (API key and shared secret).
    /// </summary>
    public bool IsLastFmConfigured =>
        !string.IsNullOrWhiteSpace(LastFmApiKey) && !string.IsNullOrWhiteSpace(LastFmSharedSecret);
}
