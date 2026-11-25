using Microsoft.IdentityModel.Tokens;

namespace WaveMP.Service.Tokens;

/// <summary>
/// Fixed values of the session tokens (JWT).
/// </summary>
public static class TokenDefaults
{
    public const string Issuer = "wavemp";

    public const string Audience = "wavemp";

    /// <summary>
    /// Audience of the tickets of the real-time connection of the devices; no other endpoint accepts them, and the
    /// connection does not accept session tokens in the query string.
    /// </summary>
    public const string DeviceHubAudience = "wavemp-device-hub";

    /// <summary>
    /// How long a ticket of the real-time connection of the devices is valid: long enough to open the connection,
    /// short enough that a copy in an access log is of no use.
    /// </summary>
    public static readonly TimeSpan DeviceHubTicketLifetime = TimeSpan.FromSeconds(60);

    public const string Algorithm = SecurityAlgorithms.HmacSha256;

    /// <summary>
    /// Name of the security scheme in the OpenAPI document.
    /// </summary>
    public const string SecuritySchemeName = "Bearer";
}
