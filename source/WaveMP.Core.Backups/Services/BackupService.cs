using Microsoft.Extensions.Logging;
using WaveMP.Core.Backups.Exports;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Queries;

namespace WaveMP.Core.Backups.Services;

internal sealed class BackupService(
    IBackupStore store,
    IDatabaseExporter exporter,
    ISettingsSnapshot<BackupSettings> settings,
    TimeProvider timeProvider,
    ILogger<BackupService> logger) : IBackupService, IDisposable
{
    private readonly SemaphoreSlim _exportLock = new(1, 1);

    public Page<BackupFile> List(int offset, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        IReadOnlyList<BackupFile> files = store.List();
        return new Page<BackupFile>([.. files.Skip(offset).Take(limit)], offset, limit, files.Count);
    }

    public async Task<BackupResult> CreateAsync(BackupTrigger trigger, CancellationToken cancellationToken = default)
    {
        await _exportLock.WaitAsync(cancellationToken);
        try
        {
            string id = store.Prepare(timeProvider.GetUtcNow(), trigger);
            logger.LogInformation("Exporting the database to {BackupFile}", id + BackupFileNames.Extension);
            BackupFile created;
            try
            {
                await exporter.ExportAsync(store.PartialPathOf(id), cancellationToken);
                created = store.Complete(id);
            }
            catch
            {
                store.Discard(id);
                throw;
            }

            logger.LogInformation(
                "Exported the database to {BackupFile} ({SizeBytes} bytes)",
                created.FileName,
                created.SizeBytes);

            IReadOnlyList<BackupFile> deleted = [];
            if (trigger != BackupTrigger.Migration)
            {
                deleted = store.ApplyRetention(settings.Current.RetentionCount);
                foreach (BackupFile file in deleted)
                {
                    logger.LogInformation("Deleted the old export {BackupFile}", file.FileName);
                }
            }

            return new BackupResult(created, deleted);
        }
        finally
        {
            _exportLock.Release();
        }
    }

    public bool Delete(string id)
    {
        bool deleted = store.Delete(id);
        if (deleted)
        {
            logger.LogInformation("Deleted the export {BackupId}", id);
        }

        return deleted;
    }

    public void Dispose()
    {
        _exportLock.Dispose();
    }
}
