using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Users.Sessions;

namespace WaveMP.Service.Hubs;

/// <summary>
/// Closes the real-time connections of sessions that ended, so a device of a profile that was switched, deleted or
/// protected with a PIN gets nothing more of the profile.
/// </summary>
public sealed class DeviceHubSessionCloser(
    IDeviceCoordinator coordinator,
    DeviceHubConnections connections,
    IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// Closes the connections of a session at once, e.g. because it was ended.
    /// </summary>
    public async Task CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await coordinator.AbortSessionAsync(sessionId, cancellationToken);
        connections.AbortSession(sessionId);
    }

    /// <summary>
    /// Closes the connections of a profile at once, e.g. because it was deleted.
    /// </summary>
    public async Task CloseUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await coordinator.AbortUserAsync(userId, cancellationToken);
        connections.AbortUser(userId);
    }

    /// <summary>
    /// Closes the connections whose session is no longer active (expired, or ended without this server noticing, e.g.
    /// when a PIN was set). Returns the number of sessions that ended.
    /// </summary>
    public async Task<int> CloseEndedSessionsAsync(CancellationToken cancellationToken = default)
    {
        HashSet<Guid> sessionIds = [.. connections.SessionIds];
        sessionIds.UnionWith(await coordinator.GetConnectedSessionIdsAsync(cancellationToken));
        if (sessionIds.Count == 0)
        {
            return 0;
        }

        int ended = 0;
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ISessionService sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();
        foreach (Guid sessionId in sessionIds)
        {
            if (await sessions.GetActiveAsync(sessionId, cancellationToken) is null)
            {
                await CloseSessionAsync(sessionId, cancellationToken);
                ended++;
            }
        }

        return ended;
    }
}
