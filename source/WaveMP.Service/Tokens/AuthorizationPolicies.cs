namespace WaveMP.Service.Tokens;

/// <summary>
/// Names of the authorization policies.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Only profiles with the role admin.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Endpoints that deliver media: a session token or a signed media URL (see
    /// <see cref="Signatures.SignedMediaAttribute"/>).
    /// </summary>
    public const string Media = "Media";

    /// <summary>
    /// The real-time connection of the devices: a session token in the header Authorization or a ticket of the
    /// connection (in the query parameter <c>access_token</c> or the header), see
    /// <see cref="Hubs.DeviceHubDefaults"/>.
    /// </summary>
    public const string DeviceHub = "DeviceHub";
}
