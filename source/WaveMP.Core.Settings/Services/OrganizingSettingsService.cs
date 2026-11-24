using Microsoft.Extensions.Logging;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Core.Settings.Stores;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Services;

internal sealed class OrganizingSettingsService(
    ISettingsStore store,
    SettingsSnapshot<OrganizingSettings> snapshot,
    IEnumerable<ISettingsChangeHandler<OrganizingSettings>> handlers,
    ILogger<OrganizingSettingsService> logger) : IOrganizingSettingsService
{
    public async Task<OrganizingSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        OrganizingSettings stored = OrganizingSettingsValidator.Normalize(
            await store.GetAsync<OrganizingSettings>(OrganizingSettings.Key, cancellationToken));
        if (OrganizingSettingsValidator.Validate(stored).Count == 0)
        {
            return stored;
        }

        // Only possible if the stored document was changed outside of WaveMP.
        logger.LogWarning("The saved settings of the organization are invalid; using the defaults");
        return new OrganizingSettings();
    }

    public async Task<OrganizingSettingsResult> UpdateAsync(
        UpdateOrganizingSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        await snapshot.UpdateLock.WaitAsync(cancellationToken);
        try
        {
            OrganizingSettings current = await GetAsync(cancellationToken);
            OrganizingSettings changed = OrganizingSettingsValidator.Normalize(new OrganizingSettings
            {
                LayoutTemplate = command.LayoutTemplate ?? current.LayoutTemplate,
            });

            IReadOnlyList<OrganizingSettingsValidationError> errors = OrganizingSettingsValidator.Validate(changed);
            if (errors.Count > 0)
            {
                return OrganizingSettingsResult.Failure(errors);
            }

            await store.SaveAsync(OrganizingSettings.Key, changed, cancellationToken);
            OrganizingSettings previous = snapshot.Current;
            snapshot.Set(changed);
            if (!string.Equals(current.LayoutTemplate, changed.LayoutTemplate, StringComparison.Ordinal))
            {
                logger.LogInformation("Layout template changed to {LayoutTemplate}", changed.LayoutTemplate);
            }

            foreach (ISettingsChangeHandler<OrganizingSettings> handler in handlers)
            {
                try
                {
                    await handler.OnSettingsChangedAsync(previous, changed, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(
                        exception,
                        "{Handler} failed to apply the changed settings of the organization",
                        handler.GetType().Name);
                }
            }

            return OrganizingSettingsResult.Success(changed);
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
