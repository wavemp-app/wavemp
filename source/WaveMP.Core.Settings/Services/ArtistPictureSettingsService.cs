using Microsoft.Extensions.Logging;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Core.Settings.Stores;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Services;

/// <summary>
/// The settings of the artist pictures. The API key of fanart.tv is a secret: the log only says whether it was set or
/// removed, never the key.
/// </summary>
internal sealed class ArtistPictureSettingsService(
    ISettingsStore store,
    SettingsSnapshot<ArtistPictureSettings> snapshot,
    IEnumerable<ISettingsChangeHandler<ArtistPictureSettings>> handlers,
    ILogger<ArtistPictureSettingsService> logger) : IArtistPictureSettingsService
{
    public async Task<ArtistPictureSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        ArtistPictureSettings stored = ArtistPictureSettingsValidator.Normalize(
            await store.GetAsync<ArtistPictureSettings>(ArtistPictureSettings.Key, cancellationToken));
        if (ArtistPictureSettingsValidator.Validate(stored).Count == 0)
        {
            return stored;
        }

        // Only possible if the stored document was changed outside of WaveMP. The defaults keep the lookup off.
        logger.LogWarning("The saved settings of the artist pictures are invalid; using the defaults");
        return new ArtistPictureSettings();
    }

    public async Task<ArtistPictureSettingsResult> UpdateAsync(
        UpdateArtistPictureSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        await snapshot.UpdateLock.WaitAsync(cancellationToken);
        try
        {
            ArtistPictureSettings current = await GetAsync(cancellationToken);
            ArtistPictureSettings changed = ArtistPictureSettingsValidator.Normalize(new ArtistPictureSettings
            {
                OnlineLookupEnabled = command.OnlineLookupEnabled ?? current.OnlineLookupEnabled,
                FanartTvApiKey = command.FanartTvApiKey ?? current.FanartTvApiKey,
            });

            IReadOnlyList<ArtistPictureSettingsValidationError> errors =
                ArtistPictureSettingsValidator.Validate(changed);
            if (errors.Count > 0)
            {
                return ArtistPictureSettingsResult.Failure(errors);
            }

            await store.SaveAsync(ArtistPictureSettings.Key, changed, cancellationToken);
            ArtistPictureSettings previous = snapshot.Current;
            snapshot.Set(changed);
            if (current.OnlineLookupEnabled != changed.OnlineLookupEnabled)
            {
                logger.LogInformation(
                    "Online lookup of artist pictures switched {State}",
                    changed.OnlineLookupEnabled ? "on" : "off");
            }

            if (!string.Equals(current.FanartTvApiKey, changed.FanartTvApiKey, StringComparison.Ordinal))
            {
                // Only whether there is a key; the key itself never goes into the log.
                logger.LogInformation(
                    "API key of fanart.tv {Change}",
                    changed.FanartTvApiKey is null ? "removed" : "set");
            }

            foreach (ISettingsChangeHandler<ArtistPictureSettings> handler in handlers)
            {
                try
                {
                    await handler.OnSettingsChangedAsync(previous, changed, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(
                        exception,
                        "{Handler} failed to apply the changed settings of the artist pictures",
                        handler.GetType().Name);
                }
            }

            return ArtistPictureSettingsResult.Success(changed);
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
