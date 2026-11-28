using Microsoft.Extensions.Logging;
using Quartz;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Analysis.Jobs;

/// <summary>
/// The song analysis as background job, started after scans that changed files (and after every full scan, which also
/// starts the first analysis of an existing library), when an admin asks for a song or the whole library to be
/// analyzed again, and by an admin; it has no schedule of its own. Never runs in parallel to itself. The analysis looks
/// up the lyrics of every analyzed track in the order its detection needs them; afterwards, if the download of lyrics
/// is switched on, it starts the job <see cref="JobIds.LyricsDownload"/> for the tracks that are still left.
/// </summary>
[DisallowConcurrentExecution]
public sealed class SongAnalysisJob(
    ISongAnalysisRunner runner,
    IJobService jobs,
    ISettingsSnapshot<LibrarySettings> settings,
    ILogger<SongAnalysisJob> logger) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        context.Result = await runner.RunAsync(cancellationToken);
        if (!settings.Current.DownloadLyrics)
        {
            return;
        }

        try
        {
            JobRunStartResult started = await jobs.StartRunAsync(
                new JobRunRequest(JobIds.LyricsDownload, JobRunTrigger.LibraryScan),
                CancellationToken.None);
            if (!started.IsSuccess)
            {
                logger.LogDebug("The lyrics download is not started after the song analysis: {Reason}", started.Error);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot start the lyrics download after the song analysis");
        }
    }
}
