using System.Collections.Concurrent;
using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Playbacks;

namespace WaveMP.Core.Devices.Tests.Fakes;

/// <summary>
/// Records every message the coordinator sends, per connection.
/// </summary>
internal sealed class RecordingNotifier : IDeviceNotifier
{
    public ConcurrentQueue<(string ConnectionId, object Message)> Messages { get; } = new();

    public ConcurrentQueue<string> Aborted { get; } = new();

    public ConcurrentQueue<string> Replaced { get; } = new();

    /// <summary>
    /// The messages of one type a connection got, in order.
    /// </summary>
    public IReadOnlyList<T> Of<T>(string connectionId)
    {
        return [.. Messages.Where(entry => entry.ConnectionId == connectionId).Select(entry => entry.Message).OfType<T>()];
    }

    public void Clear()
    {
        Messages.Clear();
        Aborted.Clear();
        Replaced.Clear();
    }

    public Task DevicesChangedAsync(
        IReadOnlyCollection<string> connectionIds,
        IReadOnlyList<DeviceView> devices,
        CancellationToken cancellationToken)
    {
        foreach (string connectionId in connectionIds)
        {
            Messages.Enqueue((connectionId, devices));
        }

        return Task.CompletedTask;
    }

    public Task PlaybackChangedAsync(
        IReadOnlyCollection<string> connectionIds,
        LivePlaybackView playback,
        CancellationToken cancellationToken)
    {
        foreach (string connectionId in connectionIds)
        {
            Messages.Enqueue((connectionId, playback));
        }

        return Task.CompletedTask;
    }

    public Task CommandReceivedAsync(string connectionId, ReceivedCommand command, CancellationToken cancellationToken)
    {
        Messages.Enqueue((connectionId, command));
        return Task.CompletedTask;
    }

    public Task DeviceReplacedAsync(string connectionId, CancellationToken cancellationToken)
    {
        Replaced.Enqueue(connectionId);
        return Task.CompletedTask;
    }

    public Task AbortAsync(string connectionId, CancellationToken cancellationToken)
    {
        Aborted.Enqueue(connectionId);
        return Task.CompletedTask;
    }
}
