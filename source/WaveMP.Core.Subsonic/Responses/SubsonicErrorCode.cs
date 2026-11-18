namespace WaveMP.Core.Subsonic.Responses;

/// <summary>
/// The error codes of the Subsonic API with the OpenSubsonic additions; the value is the code of the response.
/// </summary>
public enum SubsonicErrorCode
{
    /// <summary>
    /// A generic error.
    /// </summary>
    Generic = 0,

    /// <summary>
    /// A required parameter is missing.
    /// </summary>
    MissingParameter = 10,

    /// <summary>
    /// The client speaks an older protocol than the server supports.
    /// </summary>
    ClientMustUpgrade = 20,

    /// <summary>
    /// The client speaks a newer protocol than the server supports.
    /// </summary>
    ServerMustUpgrade = 30,

    /// <summary>
    /// Wrong user name or password.
    /// </summary>
    WrongCredentials = 40,

    /// <summary>
    /// The token method is not supported for the user (an LDAP user in Subsonic; never sent by WaveMP).
    /// </summary>
    TokenNotSupported = 41,

    /// <summary>
    /// The way of signing in is not supported.
    /// </summary>
    AuthenticationNotSupported = 42,

    /// <summary>
    /// More than one way of signing in at once.
    /// </summary>
    ConflictingAuthentication = 43,

    /// <summary>
    /// The API key is not valid.
    /// </summary>
    InvalidApiKey = 44,

    /// <summary>
    /// The user may not do this.
    /// </summary>
    NotAuthorized = 50,

    /// <summary>
    /// The requested data does not exist.
    /// </summary>
    NotFound = 70,
}
