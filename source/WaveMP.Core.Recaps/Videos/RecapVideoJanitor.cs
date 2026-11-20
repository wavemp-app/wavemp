using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Recaps.Options;

namespace WaveMP.Core.Recaps.Videos;

/// <summary>
/// Cleans the folder of the recap videos after the start and then regularly.
/// </summary>
internal sealed class RecapVideoJanitor(
    RecapVideoCleaner cleaner,
    IOptions<RecapOptions> options,
    TimeProvider timeProvider,
    ILogger<RecapVideoJanitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The start of the application goes first.
        await Task.Yield();
        CleanUp();
        using PeriodicTimer timer = new(options.Value.CleanupInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                CleanUp();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private void CleanUp()
    {
        try
        {
            int deleted = cleaner.CleanUp();
            if (deleted > 0)
            {
                logger.LogInformation("Deleted {Count} left-over files of recap videos", deleted);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Cleaning up the recap videos failed");
        }
    }
}
