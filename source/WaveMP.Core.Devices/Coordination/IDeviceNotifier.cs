using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Playbacks;

namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// Sends the messages of the server to connections of devices. The host implements it with its real-time transport;
/// a message to a connection that closed in the meantime is dropped.
/// </summary>
public interface IDeviceNotifier
{
    /// <summary>
    /// The devices of the profile changed: one opened or closed, was renamed or forgotten, allowed or forbade remote
    /// control, or the active device changed.
    /// </summary>
    Task DevicesChangedAsync(
        IReadOnlyCollection<string> connectionIds,
        IReadOnlyList<DeviceView> devices,
        CancellationToken cancellationToken);

    /// <summary>
    /// The live playback of the profile changed.
    /// </summary>
    Task PlaybackChangedAsync(
        IReadOnlyCollection<string> connectionIds,
        LivePlaybackView playback,
        CancellationToken cancellationToken);

    /// <summary>
    /// A remote sent a command to the device of this connection.
    /// </summary>
    Task CommandReceivedAsync(string connectionId, ReceivedCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// A newer connection registered the same device; this connection no longer takes part.
    /// </summary>
    Task DeviceReplacedAsync(string connectionId, CancellationToken cancellationToken);

    /// <summary>
    /// Closes the connection, e.g. because its session ended.
    /// </summary>
    Task AbortAsync(string connectionId, CancellationToken cancellationToken);
}
