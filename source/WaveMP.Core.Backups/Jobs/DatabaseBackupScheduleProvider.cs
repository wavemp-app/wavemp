using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;

namespace WaveMP.Core.Backups.Jobs;

/// <summary>
/// The daily backup at the time of the backup settings, in the local time zone of the server (<c>TZ</c>).
/// </summary>
public sealed class DatabaseBackupScheduleProvider(
    ISettingsSnapshot<BackupSettings> settings,
    TimeProvider timeProvider) : IJobScheduleProvider
{
    public string JobId => JobIds.DatabaseBackup;

    public JobSchedule? GetSchedule(JobScheduleReason reason)
    {
        return JobSchedule.Daily(settings.Current.ScheduleTime, timeProvider.LocalTimeZone);
    }
}
