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
/// The library scan as background job: scans all active media sources, or the folders named in the payload (a JSON
/// list of folders of sources, sent by the file watcher and after the admin added or changed a source). Never runs in parallel to itself; every run is built in a
/// dependency injection scope of its own. The result becomes part of the run history. After a full scan, and after a
/// partial scan that changed files, it starts the job <see cref="JobIds.CoverCache"/>, which prepares the covers of
/// the changed library, and, if the download of lyrics is switched on, the job <see cref="JobIds.LyricsDownload"/>,
/// which looks up the lyrics of new tracks. After every scan it starts the job <see cref="JobIds.LibraryCheck"/>,
/// which looks for problems in the library.
/// </summary>
[DisallowConcurrentExecution]
public sealed class LibraryScanJob(
    ILibraryScanner scanner,
    IJobRunTracker tracker,
    IJobService jobs,
    ISettingsSnapshot<LibrarySettings> settings,
    ILogger<LibraryScanJob> logger) : IJob
{
    private static readonly JsonSerializerOptions _payloadOptions = new(JsonSerializerDefaults.Web);

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

        // Also after scans without changes: the check also sees what changed since its last run without a scan, such
        // as tags written by the metadata editor and covers from the online search.
        await StartFollowUpAsync(JobIds.LibraryCheck);
    }

    /// <summary>
    /// The folders of the payload, or all active sources.
    /// </summary>
    internal static ScanScope ReadScope(JobDataMap data)
    {
        if (!data.TryGetValue(JobDataKeys.Payload, out object? value) || value is not string { Length: > 0 } json)
        {
            return ScanScope.Full;
        }

        try
        {
            SourceFolder[]? folders = JsonSerializer.Deserialize<SourceFolder[]>(json, _payloadOptions);
            return folders is { Length: > 0 } && folders.All(folder => folder is { Path: not null })
                ? ScanScope.Of(folders)
                : ScanScope.Full;
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
        return JsonSerializer.Serialize(scope.Folders, _payloadOptions);
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
