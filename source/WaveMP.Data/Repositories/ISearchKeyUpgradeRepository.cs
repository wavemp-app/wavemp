namespace WaveMP.Data.Repositories;

/// <summary>
/// The upgrade of an existing library to the search keys of <see cref="Texts.SearchKeys"/>, which the service runs once
/// after the migration that added them (see <see cref="Entities.DataUpgrade.SearchKeys"/>). New and renamed entries get
/// their keys when they are saved; this sets them for the entries that existed before.
/// </summary>
public interface ISearchKeyUpgradeRepository
{
    /// <summary>
    /// Whether the upgrade is pending: the migration added it and it has not finished yet.
    /// </summary>
    Task<bool> IsPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the search key of every artist, album, track and alias from its name or title and marks the upgrade as
    /// finished, all in one transaction, so a failure changes nothing and the next start tries again. Returns the
    /// number of entries.
    /// </summary>
    Task<int> ApplyAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
