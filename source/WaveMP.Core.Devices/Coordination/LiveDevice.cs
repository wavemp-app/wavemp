using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// An open device of a profile.
/// </summary>
internal sealed class LiveDevice(string id, string connectionId, string name, DeviceType type, bool remoteControl)
{
    public string Id { get; } = id;

    public string ConnectionId { get; } = connectionId;

    /// <summary>
    /// The name to show: the one the user gave it, otherwise the one it reports.
    /// </summary>
    public string Name { get; set; } = name;

    public DeviceType Type { get; } = type;

    /// <summary>
    /// Whether the user allowed remote control on the running page.
    /// </summary>
    public bool RemoteControl { get; set; } = remoteControl;

    /// <summary>
    /// When its last activity was written.
    /// </summary>
    public DateTimeOffset? TouchedAt { get; set; }
}
