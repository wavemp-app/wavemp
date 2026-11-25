using System.Globalization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WaveMP.Core.Users.Sessions;

namespace WaveMP.Service.Tokens;

/// <summary>
/// Issues session tokens as JWT signed with HMAC-SHA256. The token only names the profile (<c>sub</c>) and the
/// session (<c>sid</c>); every request checks the session and reads the role from the database. The tickets of the
/// real-time connection of the devices are JWT with the same key, the same claims, a unique id (<c>jti</c>), their own
/// audience and a lifetime of a minute.
/// </summary>
public sealed class JwtTokenIssuer(SigningKeyStore keys, TimeProvider timeProvider) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public string Issue(ActiveSession session)
    {
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = TokenDefaults.Issuer,
            Audience = TokenDefaults.Audience,
            Claims = new Dictionary<string, object>
            {
                [WaveMpClaimTypes.Subject] = session.User.Id.ToString("D", CultureInfo.InvariantCulture),
                [WaveMpClaimTypes.SessionId] = session.Session.Id.ToString("D", CultureInfo.InvariantCulture),
            },
            IssuedAt = session.Session.CreatedAt.UtcDateTime,
            NotBefore = session.Session.CreatedAt.UtcDateTime,
            Expires = session.Session.ExpiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(keys.Key, TokenDefaults.Algorithm),
        };

        return _handler.CreateToken(descriptor);
    }

    public DeviceHubTicket IssueDeviceHubTicket(Guid userId, Guid sessionId)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + TokenDefaults.DeviceHubTicketLifetime;
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = TokenDefaults.Issuer,
            Audience = TokenDefaults.DeviceHubAudience,
            Claims = new Dictionary<string, object>
            {
                [WaveMpClaimTypes.Subject] = userId.ToString("D", CultureInfo.InvariantCulture),
                [WaveMpClaimTypes.SessionId] = sessionId.ToString("D", CultureInfo.InvariantCulture),
                [WaveMpClaimTypes.TokenId] = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            },
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(keys.Key, TokenDefaults.Algorithm),
        };

        return new DeviceHubTicket(_handler.CreateToken(descriptor), expiresAt);
    }
}
