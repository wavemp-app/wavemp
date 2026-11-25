namespace WaveMP.Service.Hubs;

/// <summary>
/// Fixed values of the real-time connection of the devices (asyncapi.yaml).
/// </summary>
public static class DeviceHubDefaults
{
    /// <summary>
    /// Path of the connection. Below <c>/api</c>, so the web app fallback and the policy of the web app leave it
    /// alone.
    /// </summary>
    public const string Path = "/api/v1/hubs/devices";

    /// <summary>
    /// Authentication scheme of the tickets of the connection (see <c>CreateDeviceHubTicket</c>).
    /// </summary>
    public const string AuthenticationScheme = "DeviceHubTicket";

    /// <summary>
    /// Query parameter that carries a ticket when the client cannot send a header (WebSockets and Server-Sent Events
    /// in browsers). Session tokens are never accepted there.
    /// </summary>
    public const string AccessTokenQueryParameter = "access_token";

    /// <summary>
    /// Error of a call whose session ended (the problem code of a 401 of the API); the connection is closed.
    /// </summary>
    public const string SessionEndedError = "auth.unauthorized";

    /// <summary>
    /// Largest message a client may send; a larger one closes the connection. A publication with the largest queue
    /// (2000 entries) or a command with the most songs (2000) needs less than half of it.
    /// </summary>
    public const long MaximumReceiveMessageSize = 256 * 1024;

    /// <summary>
    /// How often the server sends a ping when nothing else was sent.
    /// </summary>
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long the server waits for a message (or ping) of the client before it closes the connection.
    /// </summary>
    public static readonly TimeSpan ClientTimeoutInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How often the server closes the connections of sessions that ended (expired or ended elsewhere) and forgets the
    /// live state of profiles without devices.
    /// </summary>
    public static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(60);
}
