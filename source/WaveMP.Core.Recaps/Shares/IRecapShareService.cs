using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Recaps.Shares;

/// <summary>
/// Recaps the profiles share with the other profiles of the instance. Nothing is shared unless a profile shares a recap
/// itself; every profile sees all recaps the others shared.
/// </summary>
public interface IRecapShareService
{
    /// <summary>
    /// Lists the recaps the user shared (<paramref name="own"/>) or the other profiles shared, the most recently shared
    /// first.
    /// </summary>
    Task<Page<RecapShareRow>> ListAsync(
        Guid userId,
        bool own,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A shared recap of any profile, or <c>null</c>.
    /// </summary>
    Task<RecapShareRow?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shares a recap; sharing the same period again gives the existing share the new design.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user does not exist (any more).</exception>
    Task<RecapShareSaveResult> ShareAsync(RecapShareRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes back a recap the user shared.
    /// </summary>
    Task<RecapShareDeletion> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A shared recap with the statistics of its profile, as <paramref name="viewerId"/> sees them; <c>null</c> if it
    /// does not exist or its time zone is unknown.
    /// </summary>
    Task<SharedRecap?> GetStatisticsAsync(
        Guid id,
        Guid viewerId,
        int top,
        CancellationToken cancellationToken = default);
}
