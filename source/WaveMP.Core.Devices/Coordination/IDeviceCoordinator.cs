using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Playbacks;

namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// The live devices of all profiles and their playback, the single source of truth while devices are connected. Each
/// profile has its own state; a connection only sees and controls the devices of the profile of its session. Exactly
/// one device per profile is the active playback device; the others are remotes. Every accepted change raises the
/// version of the state. The state is kept in memory on this server.
/// </summary>
public interface IDeviceCoordinator
{
    /// <summary>
    /// Registers the device of a connection. A device id registered by another connection of the profile moves to
    /// this connection; the other one gets <see cref="IDeviceNotifier.DeviceReplacedAsync"/>. The other devices of the
    /// profile get the new list of devices.
    /// </summary>
    /// <exception cref="WaveMP.Data.Repositories.UserNotFoundException">The profile was deleted.</exception>
    Task<RegisterResult> RegisterAsync(
        DeviceConnection connection,
        RegisterDeviceCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The connection closed; its device is no longer open. It stays the active device if it was, so it continues
    /// when it comes back, unless another device takes over in the meantime.
    /// </summary>
    Task DisconnectAsync(DeviceConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// The device of the connection allows or forbids remote control (for the running page only).
    /// </summary>
    Task<CommandResult> SetRemoteControlAsync(
        DeviceConnection connection,
        bool allowed,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The device of the connection publishes the state of its player. Accepted from the active device, and from
    /// another device that claims playback because the user started it there; that device becomes the active one.
    /// </summary>
    Task<PublishResult> PublishAsync(
        DeviceConnection connection,
        PublishPlaybackCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Passes a command of the device of the connection on to the active device, if that is open and allows remote
    /// control. Commands on entries of the queue need the current queue version.
    /// </summary>
    Task<CommandResult> SendCommandAsync(
        DeviceConnection connection,
        DeviceCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves playback to another open device that allows remote control, or to the device of the connection itself,
    /// at the same position. Taking playback away from another device needs its permission too, unless it is closed.
    /// The live state is saved as the playback state of the profile, so the new device continues from it.
    /// </summary>
    Task<CommandResult> TransferAsync(
        DeviceConnection connection,
        TransferPlaybackCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live queue with its songs, for the device of the connection.
    /// </summary>
    Task<DeviceQueueResult> GetQueueAsync(DeviceConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the connections of a session that ended.
    /// </summary>
    Task AbortSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes all connections of a profile, e.g. because it was deleted.
    /// </summary>
    Task AbortUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The sessions with registered connections, to check whether they are still active.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetConnectedSessionIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the live state of profiles whose last connection closed more than
    /// <see cref="Validations.DeviceLimits.IdleSessionLifetime"/> ago.
    /// </summary>
    Task RemoveIdleSessionsAsync(CancellationToken cancellationToken = default);
}
