using Microsoft.Extensions.Logging;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Core.Settings.Stores;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Services;

internal sealed class OnlineMetadataSettingsService(
    ISettingsStore store,
    SettingsSnapshot<OnlineMetadataSettings> snapshot,
    IEnumerable<ISettingsChangeHandler<OnlineMetadataSettings>> handlers,
    ILogger<OnlineMetadataSettingsService> logger) : IOnlineMetadataSettingsService
{
    public async Task<OnlineMetadataSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        OnlineMetadataSettings stored = OnlineMetadataSettingsValidator.Normalize(
            await store.GetAsync<OnlineMetadataSettings>(OnlineMetadataSettings.Key, cancellationToken));
        if (OnlineMetadataSettingsValidator.Validate(stored).Count == 0)
        {
            return stored;
        }

        // Only possible if the stored document was changed outside of WaveMP. The defaults keep the search off.
        logger.LogWarning("The saved settings of the online search are invalid; using the defaults");
        return new OnlineMetadataSettings();
    }

    public async Task<OnlineMetadataSettingsResult> UpdateAsync(
        UpdateOnlineMetadataSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        await snapshot.UpdateLock.WaitAsync(cancellationToken);
        try
        {
            OnlineMetadataSettings current = await GetAsync(cancellationToken);
            OnlineMetadataSettings changed = OnlineMetadataSettingsValidator.Normalize(new OnlineMetadataSettings
            {
                Enabled = command.Enabled ?? current.Enabled,
                MusicBrainzUrl = command.MusicBrainzUrl ?? current.MusicBrainzUrl,
                CoverArtArchiveUrl = command.CoverArtArchiveUrl ?? current.CoverArtArchiveUrl,
                MusicBrainzRequestIntervalMilliseconds = command.MusicBrainzRequestIntervalMilliseconds
                    ?? current.MusicBrainzRequestIntervalMilliseconds,
            });

            IReadOnlyList<OnlineMetadataSettingsValidationError> errors =
                OnlineMetadataSettingsValidator.Validate(changed);
            if (errors.Count > 0)
            {
                return OnlineMetadataSettingsResult.Failure(errors);
            }

            await store.SaveAsync(OnlineMetadataSettings.Key, changed, cancellationToken);
            OnlineMetadataSettings previous = snapshot.Current;
            snapshot.Set(changed);

            foreach (ISettingsChangeHandler<OnlineMetadataSettings> handler in handlers)
            {
                try
                {
                    await handler.OnSettingsChangedAsync(previous, changed, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(
                        exception,
                        "{Handler} failed to apply the changed settings of the online search",
                        handler.GetType().Name);
                }
            }

            return OnlineMetadataSettingsResult.Success(changed);
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
