using WaveMP.Core.Users.Sessions;

namespace WaveMP.Service.Tokens;

/// <summary>
/// Issues the token of a session and the tickets of the real-time connection of the devices.
/// </summary>
public interface ITokenIssuer
{
    string Issue(ActiveSession session);

    /// <summary>
    /// Issues a ticket that opens the real-time connection of the devices for the session of
    /// <paramref name="userId"/>, valid for <see cref="TokenDefaults.DeviceHubTicketLifetime"/>. It is accepted by
    /// nothing else (audience <see cref="TokenDefaults.DeviceHubAudience"/>).
    /// </summary>
    DeviceHubTicket IssueDeviceHubTicket(Guid userId, Guid sessionId);
}
