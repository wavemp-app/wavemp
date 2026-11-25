using WaveMP.Service.Dtos;

namespace WaveMP.Service.Hubs;

/// <summary>
/// The messages the server sends to the devices over the real-time connection (asyncapi.yaml). The method names are the
/// targets of the invocations.
/// </summary>
public interface IDeviceHubClient
{
    /// <summary>
    /// The devices of the profile changed. Sent to every registered device of the profile.
    /// </summary>
    Task DevicesChanged(DeviceListDto devices);

    /// <summary>
    /// The live playback of the profile changed. Sent to every registered device of the profile except the one that
    /// published it; a hand-over goes to all of them.
    /// </summary>
    Task PlaybackChanged(LivePlaybackDto playback);

    /// <summary>
    /// A remote sent a command to this device. Only the target device gets it.
    /// </summary>
    Task CommandReceived(ReceivedCommandDto command);

    /// <summary>
    /// A newer connection registered the device of this connection.
    /// </summary>
    Task DeviceReplaced(DeviceReplacedDto replaced);
}
