namespace WaveMP.Data.Entities;

/// <summary>
/// Whether the stored credentials of a connection to a scrobbling service still work.
/// </summary>
public enum ScrobbleConnectionStatus
{
    /// <summary>
    /// The connection works; played songs are reported.
    /// </summary>
    Active,

    /// <summary>
    /// The service rejected the credentials (e.g. access revoked or token reset). Nothing is sent until the user
    /// connects the service again; the queued scrobbles are kept until then.
    /// </summary>
    AuthorizationFailed,
}
