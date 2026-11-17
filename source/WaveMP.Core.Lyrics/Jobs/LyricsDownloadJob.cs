using Quartz;

namespace WaveMP.Core.Lyrics.Jobs;

/// <summary>
/// Downloads the lyrics of new tracks as background job, started after scans, when the download is switched on and by
/// an admin; it has no schedule of its own. Never runs in parallel to itself; the result becomes part of the run
/// history.
/// </summary>
[DisallowConcurrentExecution]
public sealed class LyricsDownloadJob(ILyricsDownloadRunner runner) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        context.Result = await runner.RunAsync(cancellationToken);
    }
}
