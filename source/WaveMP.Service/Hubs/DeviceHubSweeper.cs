using WaveMP.Core.Devices.Coordination;

namespace WaveMP.Service.Hubs;

/// <summary>
/// Every <see cref="DeviceHubDefaults.SweepInterval"/>: closes the real-time connections of sessions that ended and
/// forgets the live state of profiles whose devices are all closed for a while. Runs in memory like the live state
/// itself; nothing of it has to survive a restart.
/// </summary>
public sealed class DeviceHubSweeper(
    DeviceHubSessionCloser closer,
    IDeviceCoordinator coordinator,
    TimeProvider timeProvider,
    ILogger<DeviceHubSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(DeviceHubDefaults.SweepInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                int ended = await closer.CloseEndedSessionsAsync(stoppingToken);
                if (ended > 0)
                {
                    logger.LogDebug("Closed the device connections of {Count} ended sessions", ended);
                }

                await coordinator.RemoveIdleSessionsAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The next round tries again; the session is also checked on every call of the connection.
                logger.LogWarning(exception, "Closing the device connections of ended sessions failed");
            }
        }
    }
}
