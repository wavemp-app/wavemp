using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Playbacks;

namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// Sends nothing; used until the host registers the notifier of its real-time transport.
/// </summary>
internal sealed class NullDeviceNotifier : IDeviceNotifier
{
    public Task DevicesChangedAsync(
        IReadOnlyCollection<string> connectionIds,
        IReadOnlyList<DeviceView> devices,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task PlaybackChangedAsync(
        IReadOnlyCollection<string> connectionIds,
        LivePlaybackView playback,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task CommandReceivedAsync(string connectionId, ReceivedCommand command, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task DeviceReplacedAsync(string connectionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task AbortAsync(string connectionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
