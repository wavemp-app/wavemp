using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quartz;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Jobs;

/// <summary>
/// The library scan as background job: scans the whole media folder, or the folders named in the payload (a JSON
/// list of relative paths, sent by the file watcher). Never runs in parallel to itself; every run is built in a
/// dependency injection scope of its own. The result becomes part of the run history. After a full scan, and after a
/// partial scan that changed files, it starts the job <see cref="JobIds.CoverCache"/>, which prepares the covers of
/// the changed library, and, if the download of lyrics is switched on, the job <see cref="JobIds.LyricsDownload"/>,
/// which looks up the lyrics of new tracks.
/// </summary>
[DisallowConcurrentExecution]
public sealed class LibraryScanJob(
    ILibraryScanner scanner,
    IJobRunTracker tracker,
    IJobService jobs,
    ISettingsSnapshot<LibrarySettings> settings,
    ILogger<LibraryScanJob> logger) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        Guid runId = tracker.FindRunId(context.FireInstanceId) ?? Guid.CreateVersion7();
        ScanScope scope = ReadScope(context.MergedJobDataMap);
        ScanResult result = await scanner.ScanAsync(scope, runId, cancellationToken);
        context.Result = result;

        if (ShouldPrepareCovers(scope, result))
        {
            await StartFollowUpAsync(JobIds.CoverCache);
            if (settings.Current.DownloadLyrics)
            {
                await StartFollowUpAsync(JobIds.LyricsDownload);
            }
        }
    }

    /// <summary>
    /// The folders of the payload, or the whole media folder.
    /// </summary>
    internal static ScanScope ReadScope(JobDataMap data)
    {
        if (!data.TryGetValue(JobDataKeys.Payload, out object? value) || value is not string { Length: > 0 } json)
        {
            return ScanScope.Full;
        }

        try
        {
            string[]? directories = JsonSerializer.Deserialize<string[]>(json);
            return directories is { Length: > 0 } ? ScanScope.Of(directories) : ScanScope.Full;
        }
        catch (JsonException)
        {
            return ScanScope.Full;
        }
    }

    /// <summary>
    /// The payload for a scan of the folders.
    /// </summary>
    public static string CreatePayload(ScanScope scope)
    {
        return JsonSerializer.Serialize(scope.Directories);
    }

    /// <summary>
    /// Whether the covers are prepared after the scan: always after a full scan (it also removes the pictures of
    /// covers that are gone), after a partial scan only if it changed files.
    /// </summary>
    internal static bool ShouldPrepareCovers(ScanScope scope, ScanResult result)
    {
        ScanStatistics statistics = result.Statistics;
        return scope.IsFull || statistics.FilesAdded + statistics.FilesUpdated + statistics.FilesRemoved > 0;
    }

    /// <summary>
    /// Queues a job that follows the scan, e.g. the preparation of the covers. The scan stays successful if it cannot
    /// be started.
    /// </summary>
    private async Task StartFollowUpAsync(string jobId)
    {
        try
        {
            JobRunStartResult started = await jobs.StartRunAsync(
                new JobRunRequest(jobId, JobRunTrigger.LibraryScan),
                CancellationToken.None);
            if (!started.IsSuccess)
            {
                logger.LogDebug("The job {JobId} is not started after the scan: {Reason}", jobId, started.Error);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot start the job {JobId} after the scan", jobId);
        }
    }
}
