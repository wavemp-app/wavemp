namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// Result of checking the token of a user at a scrobbling service.
/// </summary>
public enum ScrobbleAuthorizationStatus
{
    /// <summary>
    /// The token is valid; the answer carries the account and the credentials to store.
    /// </summary>
    Authorized,

    /// <summary>
    /// The service does not accept the token (wrong, expired or not authorized by the user).
    /// </summary>
    Invalid,

    /// <summary>
    /// The service cannot be reached or does not work; the token was not checked.
    /// </summary>
    Unavailable,
}
