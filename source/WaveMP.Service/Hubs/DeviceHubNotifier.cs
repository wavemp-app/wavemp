using Microsoft.AspNetCore.SignalR;
using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Hubs;

/// <summary>
/// Sends the messages of the coordinator of the devices over the real-time connection (asyncapi.yaml).
/// </summary>
public sealed class DeviceHubNotifier(
    IHubContext<DeviceHub, IDeviceHubClient> hub,
    DeviceHubConnections connections) : IDeviceNotifier
{
    public Task DevicesChangedAsync(
        IReadOnlyCollection<string> connectionIds,
        IReadOnlyList<DeviceView> devices,
        CancellationToken cancellationToken)
    {
        return hub.Clients.Clients([.. connectionIds]).DevicesChanged(devices.ToListDto());
    }

    public Task PlaybackChangedAsync(
        IReadOnlyCollection<string> connectionIds,
        LivePlaybackView playback,
        CancellationToken cancellationToken)
    {
        return hub.Clients.Clients([.. connectionIds]).PlaybackChanged(playback.ToDto());
    }

    public Task CommandReceivedAsync(string connectionId, ReceivedCommand command, CancellationToken cancellationToken)
    {
        return hub.Clients.Client(connectionId).CommandReceived(command.ToDto());
    }

    public Task DeviceReplacedAsync(string connectionId, CancellationToken cancellationToken)
    {
        return hub.Clients.Client(connectionId).DeviceReplaced(new DeviceReplacedDto());
    }

    public Task AbortAsync(string connectionId, CancellationToken cancellationToken)
    {
        connections.Abort(connectionId);
        return Task.CompletedTask;
    }
}
