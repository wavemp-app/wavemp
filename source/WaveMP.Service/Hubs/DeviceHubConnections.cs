using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace WaveMP.Service.Hubs;

/// <summary>
/// The open connections of the real-time connection of the devices on this server, also those that have not registered
/// a device yet, so they can be closed when their session ends.
/// </summary>
public sealed class DeviceHubConnections
{
    private readonly ConcurrentDictionary<string, Entry> _connections = new(StringComparer.Ordinal);

    /// <summary>
    /// The sessions of the open connections.
    /// </summary>
    public IReadOnlyCollection<Guid> SessionIds => [.. _connections.Values.Select(entry => entry.SessionId).Distinct()];

    public void Add(HubCallerContext context, Guid userId, Guid sessionId)
    {
        _connections[context.ConnectionId] = new Entry(context, userId, sessionId);
    }

    public void Remove(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Closes a connection; nothing happens if it is closed already.
    /// </summary>
    public void Abort(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out Entry? entry))
        {
            entry.Context.Abort();
        }
    }

    /// <summary>
    /// Closes the connections of a session.
    /// </summary>
    public void AbortSession(Guid sessionId)
    {
        AbortWhere(entry => entry.SessionId == sessionId);
    }

    /// <summary>
    /// Closes the connections of a profile.
    /// </summary>
    public void AbortUser(Guid userId)
    {
        AbortWhere(entry => entry.UserId == userId);
    }

    private void AbortWhere(Func<Entry, bool> predicate)
    {
        foreach (Entry entry in _connections.Values.Where(predicate))
        {
            entry.Context.Abort();
        }
    }

    private sealed record Entry(HubCallerContext Context, Guid UserId, Guid SessionId);
}
