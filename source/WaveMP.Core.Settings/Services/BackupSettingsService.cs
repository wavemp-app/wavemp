using Microsoft.Extensions.Logging;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Core.Settings.Stores;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Services;

internal sealed class BackupSettingsService(
    ISettingsStore store,
    SettingsSnapshot<BackupSettings> snapshot,
    IEnumerable<ISettingsChangeHandler<BackupSettings>> handlers,
    ILogger<BackupSettingsService> logger) : IBackupSettingsService
{
    public async Task<BackupSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        BackupSettings stored = await store.GetAsync<BackupSettings>(BackupSettings.Key, cancellationToken);
        if (BackupSettingsValidator.Validate(stored).Count == 0)
        {
            return stored;
        }

        // Only possible if the stored document was changed outside of WaveMP.
        logger.LogWarning("The saved backup settings are invalid; using the defaults");
        return new BackupSettings();
    }

    public async Task<BackupSettingsResult> UpdateAsync(
        UpdateBackupSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        await snapshot.UpdateLock.WaitAsync(cancellationToken);
        try
        {
            BackupSettings current = await GetAsync(cancellationToken);
            BackupSettings changed = new()
            {
                ScheduleTime = command.ScheduleTime ?? current.ScheduleTime,
                RetentionCount = command.RetentionCount ?? current.RetentionCount,
            };

            IReadOnlyList<BackupSettingsValidationError> errors = BackupSettingsValidator.Validate(changed);
            if (errors.Count > 0)
            {
                return BackupSettingsResult.Failure(errors);
            }

            await store.SaveAsync(BackupSettings.Key, changed, cancellationToken);
            BackupSettings previous = snapshot.Current;
            snapshot.Set(changed);

            foreach (ISettingsChangeHandler<BackupSettings> handler in handlers)
            {
                try
                {
                    await handler.OnSettingsChangedAsync(previous, changed, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(
                        exception,
                        "{Handler} failed to apply the changed backup settings",
                        handler.GetType().Name);
                }
            }

            return BackupSettingsResult.Success(changed);
        }
        finally
        {
            snapshot.UpdateLock.Release();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        snapshot.Set(await GetAsync(cancellationToken));
    }
}
