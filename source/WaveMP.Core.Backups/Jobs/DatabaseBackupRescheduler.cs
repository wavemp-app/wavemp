using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Schedulers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;

namespace WaveMP.Core.Backups.Jobs;

/// <summary>
/// Plans the daily backup again as soon as its time of day changes. A lower number of kept exports applies with the
/// next export, so a mistaken change deletes nothing at once.
/// </summary>
public sealed class DatabaseBackupRescheduler(IJobScheduler scheduler) : ISettingsChangeHandler<BackupSettings>
{
    public async ValueTask OnSettingsChangedAsync(
        BackupSettings previous,
        BackupSettings current,
        CancellationToken cancellationToken)
    {
        if (previous.ScheduleTime != current.ScheduleTime)
        {
            await scheduler.RescheduleAsync(JobIds.DatabaseBackup, cancellationToken);
        }
    }
}
