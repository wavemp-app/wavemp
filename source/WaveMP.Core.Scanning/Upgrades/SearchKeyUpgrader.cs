using Microsoft.Extensions.Logging;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Upgrades;

/// <summary>
/// Sets the search keys of the existing artists, albums, tracks and aliases once, on the start after the migration that
/// added them (see <see cref="ISearchKeyUpgradeRepository"/>). A failed upgrade changes nothing and is repeated on the
/// next start; until it finished, the search still finds entries by their names.
/// </summary>
public sealed class SearchKeyUpgrader(
    ISearchKeyUpgradeRepository repository,
    TimeProvider timeProvider,
    ILogger<SearchKeyUpgrader> logger)
{
    /// <summary>
    /// Runs the upgrade if it is pending. Returns the number of entries, or <c>null</c> if nothing was pending.
    /// </summary>
    public async Task<int?> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!await repository.IsPendingAsync(cancellationToken))
        {
            return null;
        }

        int count = await repository.ApplyAsync(timeProvider.GetUtcNow(), cancellationToken);
        logger.LogInformation("Set the search keys of {Count} artists, albums, tracks and aliases", count);
        return count;
    }
}
