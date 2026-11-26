using Microsoft.Extensions.Logging;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Upgrades;

/// <summary>
/// Runs the upgrade to the normalized names of artists, albums and genres once, on the start after the migration that
/// added it (see <see cref="INameKeyUpgradeRepository"/> and <see cref="NameKeyUpgradePlanner"/>). The service backed
/// up the database before that migration; a failed upgrade changes nothing and is repeated on the next start.
/// </summary>
public sealed class NameKeyUpgrader(
    INameKeyUpgradeRepository repository,
    TimeProvider timeProvider,
    ILogger<NameKeyUpgrader> logger)
{
    /// <summary>
    /// Runs the upgrade if it is pending. Returns the plan it applied, or <c>null</c> if nothing was pending.
    /// </summary>
    public async Task<NameKeyUpgradePlan?> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!await repository.IsPendingAsync(cancellationToken))
        {
            return null;
        }

        NameKeyUpgradeInput input = await repository.LoadAsync(cancellationToken);
        NameKeyUpgradePlan plan = NameKeyUpgradePlanner.Plan(input);
        logger.LogInformation(
            "Upgrading {Artists} artists, {Albums} albums and {Genres} genres to normalized names: joining "
            + "{ArtistMerges} artists, {AlbumMerges} albums and {GenreMerges} genres with equal names, linking the "
            + "rules of {SmartPlaylists} smart playlists",
            input.Artists.Count,
            input.Albums.Count,
            input.Genres.Count,
            plan.ArtistMerges.Count,
            plan.AlbumMerges.Count,
            plan.GenreMerges.Count,
            plan.SmartPlaylistRules.Count);
        await repository.ApplyAsync(plan, timeProvider.GetUtcNow(), cancellationToken);
        logger.LogInformation("Artists, albums and genres use normalized names now");
        return plan;
    }
}
