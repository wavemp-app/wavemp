using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Reads what autoplay chooses the songs after a list from. Nothing is tracked.
/// </summary>
public interface IAutoplayRepository
{
    /// <summary>
    /// The tracks of the list with their artist, genre and year, in no particular order; ids of missing tracks are left
    /// out.
    /// </summary>
    Task<IReadOnlyList<AutoplaySeedRow>> ListSeedsAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The tracks autoplay may choose, see <see cref="AutoplayCandidateQuery"/>; in random order if there are more than
    /// the limit, otherwise in no particular order.
    /// </summary>
    Task<IReadOnlyList<AutoplayCandidateRow>> ListCandidatesAsync(
        AutoplayCandidateQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The tracks the user played since <paramref name="since"/>, each with the time of its last play.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListRecentPlaysAsync(
        Guid userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}
