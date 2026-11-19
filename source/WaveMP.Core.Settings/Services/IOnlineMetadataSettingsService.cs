using WaveMP.Core.Settings.Groups;

namespace WaveMP.Core.Settings.Services;

/// <summary>
/// Reads and changes the settings of the online search for album info.
/// </summary>
public interface IOnlineMetadataSettingsService
{
    Task<OnlineMetadataSettings> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks and saves the changes. Valid changes apply at once: the snapshot is replaced and every
    /// <see cref="Handlers.ISettingsChangeHandler{T}"/> is told.
    /// </summary>
    Task<OnlineMetadataSettingsResult> UpdateAsync(
        UpdateOnlineMetadataSettingsCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the saved settings into the snapshot. Runs on start, after the migrations.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
