using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The playback states of the users, one per user. Nothing is tracked.
/// </summary>
public interface IPlaybackStateRepository
{
    /// <summary>
    /// Finds the playback state of the user; <c>null</c> if none was saved.
    /// </summary>
    Task<PlaybackState?> FindAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the playback state of its user, unless the saved one changed later
    /// (<see cref="PlaybackState.ChangedAt"/>): the most recently changed state wins, also when two devices save at the
    /// same time. Returns <c>false</c> (and saves nothing) if the saved state is newer.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user of the state does not exist.</exception>
    Task<bool> SaveAsync(PlaybackState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the playback state of the user. Returns <c>false</c> if none was saved.
    /// </summary>
    Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
}
