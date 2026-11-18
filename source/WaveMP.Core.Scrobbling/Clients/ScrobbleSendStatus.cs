namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// What a scrobbling service answered to "Now Playing" or to scrobbles.
/// </summary>
public enum ScrobbleSendStatus
{
    /// <summary>
    /// The service took the request. Scrobbles it ignores (e.g. by its own filters) count as sent.
    /// </summary>
    Accepted,

    /// <summary>
    /// The service rejected the request for good, e.g. because of invalid data; sending it again does not help.
    /// </summary>
    Rejected,

    /// <summary>
    /// The service rejected the credentials of the user; the user has to connect the service again.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// The service cannot be reached, is down, refuses requests for a while or the account of WaveMP is not set up
    /// correctly; the request is sent again later.
    /// </summary>
    Unavailable,
}
