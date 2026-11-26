using Microsoft.Extensions.Logging;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Caching.Jobs;

/// <summary>
/// Brings the cache up to date after a cover rule changed (global, of an album or of a track): starts the job
/// <see cref="JobIds.CoverCache"/>, which prepares the covers shown now. The scaled pictures belong to the picture, not
/// to an album or track, so none of them becomes wrong; until the job reaches a cover, it is created when it is asked
/// for. If the job is already queued or running, no second run is started.
/// </summary>
internal sealed class CoverRuleChangeHandler(IJobService jobs, ILogger<CoverRuleChangeHandler> logger)
    : ICoverRuleChangeHandler, ISettingsChangeHandler<LibrarySettings>
{
    public async ValueTask OnCoverRuleChangedAsync(CoverRuleChange change, CancellationToken cancellationToken)
    {
        await StartPreparationAsync(cancellationToken);
    }

    public async ValueTask OnSettingsChangedAsync(
        LibrarySettings previous,
        LibrarySettings current,
        CancellationToken cancellationToken)
    {
        if (previous.TrackCoverPriority != current.TrackCoverPriority)
        {
            await StartPreparationAsync(cancellationToken);
        }
    }

    private async Task StartPreparationAsync(CancellationToken cancellationToken)
    {
        JobRunStartResult started = await jobs.StartRunAsync(
            new JobRunRequest(JobIds.CoverCache, JobRunTrigger.CoverRule),
            cancellationToken);
        if (!started.IsSuccess)
        {
            logger.LogDebug("The covers are not prepared after the change of a cover rule: {Reason}", started.Error);
        }
    }
}
