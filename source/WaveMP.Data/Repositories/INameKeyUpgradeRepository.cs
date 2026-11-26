namespace WaveMP.Data.Repositories;

/// <summary>
/// The upgrade of an existing library to the normalized names of <see cref="Texts.NameKeys"/>, which the service runs
/// once after the migration that added it (see <see cref="Entities.DataUpgrade.NameKeys"/>). Artists, albums and
/// genres get their new normalized names and keys; entries whose new names are equal are joined by the existing merge
/// logic: the songs of the joined entries move to the one that stays, with a recorded move, and
/// <see cref="ILibraryScanRepository.DeleteOrphansAsync"/> passes their favorites, cover rules, pictures, dates and
/// lists in the player on and removes them. Rules of smart playlists that name an artist, album or genre get its id.
/// </summary>
public interface INameKeyUpgradeRepository
{
    /// <summary>
    /// Whether the upgrade is pending: the migration added it and it has not finished yet.
    /// </summary>
    Task<bool> IsPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The artists, albums, genres and smart playlists, untracked.
    /// </summary>
    Task<NameKeyUpgradeInput> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the changes, joins the entries, removes the joined ones with
    /// <see cref="ILibraryScanRepository.DeleteOrphansAsync"/> and marks the upgrade as finished, all in one
    /// transaction, so a failure changes nothing and the next start tries again.
    /// </summary>
    Task ApplyAsync(NameKeyUpgradePlan plan, DateTimeOffset now, CancellationToken cancellationToken = default);
}
