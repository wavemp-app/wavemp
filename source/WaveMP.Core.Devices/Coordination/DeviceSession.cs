using WaveMP.Core.Devices.Playbacks;

namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// The live state of one profile. Only read and changed while <see cref="Lock"/> is held.
/// </summary>
internal sealed class DeviceSession(Guid userId, DateTimeOffset createdAt)
{
    public Guid UserId { get; } = userId;

    public SemaphoreSlim Lock { get; } = new(1, 1);

    /// <summary>
    /// Set when the session was dropped from the coordinator; a caller that waited for its lock starts again.
    /// </summary>
    public bool Removed { get; set; }

    /// <summary>
    /// The connections that registered a device, by their id.
    /// </summary>
    public Dictionary<string, ConnectionEntry> Connections { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The open devices by their id; each has exactly one connection.
    /// </summary>
    public Dictionary<string, LiveDevice> Devices { get; } = new(StringComparer.Ordinal);

    public string? ActiveDeviceId { get; set; }

    public LivePlayback Playback { get; set; } = LivePlayback.Empty(createdAt);

    /// <summary>
    /// Seeded with the time in Unix milliseconds, so versions keep increasing across restarts of the server and a
    /// version of an earlier state never matches by accident.
    /// </summary>
    public long Version { get; set; } = createdAt.ToUnixTimeMilliseconds();

    public long QueueVersion { get; set; } = createdAt.ToUnixTimeMilliseconds();

    /// <summary>
    /// Since when no connection is registered; <c>null</c> while one is.
    /// </summary>
    public DateTimeOffset? EmptySince { get; set; } = createdAt;

    /// <summary>
    /// The registered device of the connection; <c>null</c> if it has none or another connection replaced it.
    /// </summary>
    public LiveDevice? DeviceOf(string connectionId)
    {
        return Connections.TryGetValue(connectionId, out ConnectionEntry? entry)
            && entry.DeviceId is { } deviceId
            && Devices.TryGetValue(deviceId, out LiveDevice? device)
            && device.ConnectionId == connectionId
                ? device
                : null;
    }

    /// <summary>
    /// The connections of the open devices, except <paramref name="exceptConnectionId"/>.
    /// </summary>
    public IReadOnlyCollection<string> DeviceConnectionIds(string? exceptConnectionId = null)
    {
        return [.. Devices.Values
            .Select(device => device.ConnectionId)
            .Where(connectionId => connectionId != exceptConnectionId)];
    }

    public LivePlaybackView View(DateTimeOffset now, PlaybackHandover? handover = null)
    {
        return Playback.ToView(Version, QueueVersion, ActiveDeviceId, now, handover);
    }
}
