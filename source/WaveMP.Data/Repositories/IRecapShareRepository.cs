using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The recaps the users shared with the other profiles of the instance. Nothing is tracked.
/// </summary>
public interface IRecapShareRepository
{
    Task<Page<RecapShareRow>> ListAsync(RecapShareListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// A share of any user, or <c>null</c>.
    /// </summary>
    Task<RecapShareRow?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new share, or, if the user shared the same period (kind, days and time zone) before, gives that share
    /// the design and time of <paramref name="share"/> and keeps its id.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user of the share does not exist.</exception>
    Task<RecapShareSaveResult> SaveAsync(RecapShare share, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a share of the user; returns <c>false</c> if the user has no such share.
    /// </summary>
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
