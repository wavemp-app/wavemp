using Quartz;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Backups.Services;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Backups.Jobs;

/// <summary>
/// Exports the database as background job, every day and when an admin starts it. Never runs in parallel to itself;
/// the new file and the number of deleted old exports become part of the run history.
/// </summary>
[DisallowConcurrentExecution]
public sealed class DatabaseBackupJob(IBackupService backups) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        BackupTrigger trigger = TriggerOf(context.MergedJobDataMap);
        BackupResult result;
        try
        {
            result = await backups.CreateAsync(trigger, cancellationToken);
        }
        catch (BackupFailedException exception)
        {
            throw new JobFailedException(ToJobErrorCode(exception.Error), exception.Message, exception);
        }

        context.Result = new DatabaseBackupJobResult(
            result.Created.FileName,
            result.Created.SizeBytes,
            result.Deleted.Count);
    }

    /// <summary>
    /// Manual runs create manual exports; every other run is the daily schedule.
    /// </summary>
    internal static BackupTrigger TriggerOf(JobDataMap data)
    {
        return data.TryGetValue(JobDataKeys.Trigger, out object? value)
            && value?.ToString() == nameof(JobRunTrigger.Manual)
            ? BackupTrigger.Manual
            : BackupTrigger.Schedule;
    }

    internal static JobErrorCode ToJobErrorCode(BackupError error)
    {
        return error switch
        {
            BackupError.FolderUnwritable => JobErrorCode.BackupFolderUnwritable,
            BackupError.PgDumpUnavailable => JobErrorCode.PgDumpUnavailable,
            BackupError.ExportFailed => JobErrorCode.BackupFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Unknown backup error."),
        };
    }
}
