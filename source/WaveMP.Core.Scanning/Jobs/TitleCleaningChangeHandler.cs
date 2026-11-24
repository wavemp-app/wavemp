using Microsoft.Extensions.Logging;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Jobs;

/// <summary>
/// Starts a library scan when the layout template switches between folder-based and file-based, since that changes
/// how titles are cleaned (see <see cref="TitleCleaningModes"/>). The scan forms the tracks of every source again from
/// the stored files, without reading them. If a scan is already queued or running, none is started: the next scan
/// does it, because every source remembers the mode its titles were cleaned with.
/// </summary>
internal sealed class TitleCleaningChangeHandler(IJobService jobs, ILogger<TitleCleaningChangeHandler> logger)
    : ISettingsChangeHandler<OrganizingSettings>
{
    public async ValueTask OnSettingsChangedAsync(
        OrganizingSettings previous,
        OrganizingSettings current,
        CancellationToken cancellationToken)
    {
        if (TitleCleaningModes.Of(previous) == TitleCleaningModes.Of(current))
        {
            return;
        }

        JobRunStartResult started = await jobs.StartRunAsync(
            new JobRunRequest(JobIds.LibraryScan, JobRunTrigger.Settings),
            cancellationToken);
        if (!started.IsSuccess)
        {
            logger.LogDebug("No scan started after the title cleaning changed: {Reason}", started.Error);
        }
    }
}
