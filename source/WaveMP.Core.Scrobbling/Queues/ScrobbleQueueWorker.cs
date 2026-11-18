using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Scrobbling.Options;

namespace WaveMP.Core.Scrobbling.Queues;

/// <summary>
/// Sends the queue in the background: shortly after the start (the scrobbles left from the last run), whenever a new
/// play is queued or a service is connected, when the next postponed scrobble is due and otherwise after the idle
/// interval. The queue itself lives in the database, so nothing is lost on a restart; this is no Quartz job, so the
/// run history of the jobs is not filled with a run per song.
/// </summary>
internal sealed class ScrobbleQueueWorker(
    IServiceScopeFactory scopeFactory,
    ScrobbleQueueSignal signal,
    TimeProvider timeProvider,
    IOptions<ScrobblingOptions> options,
    ILogger<ScrobbleQueueWorker> logger) : BackgroundService
{
    /// <summary>
    /// Shortest wait between two rounds, so a queue that cannot be emptied never keeps the processor busy.
    /// </summary>
    private static readonly TimeSpan _minWait = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ScrobblingOptions settings = options.Value;
        try
        {
            if (settings.StartupDelay > TimeSpan.Zero)
            {
                await Task.Delay(settings.StartupDelay, timeProvider, stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan wait;
                try
                {
                    await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                    DateTimeOffset? next = await scope.ServiceProvider
                        .GetRequiredService<IScrobbleQueueProcessor>()
                        .ProcessAsync(stoppingToken);
                    wait = Wait(next, timeProvider.GetUtcNow(), settings);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Sending the scrobble queue failed");
                    wait = settings.RetryDelay > _minWait ? settings.RetryDelay : _minWait;
                }

                await signal.WaitAsync(wait, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    /// <summary>
    /// The wait until the next round: until the next scrobble is due, at least <see cref="_minWait"/> and at most
    /// the idle interval.
    /// </summary>
    internal static TimeSpan Wait(DateTimeOffset? next, DateTimeOffset now, ScrobblingOptions settings)
    {
        TimeSpan idle = settings.IdleInterval > _minWait ? settings.IdleInterval : _minWait;
        if (next is not { } due)
        {
            return idle;
        }

        TimeSpan wait = due - now;
        return wait < _minWait ? _minWait : wait > idle ? idle : wait;
    }
}
