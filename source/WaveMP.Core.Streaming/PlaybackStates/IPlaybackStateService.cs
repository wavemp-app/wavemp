namespace WaveMP.Core.Streaming.PlaybackStates;

/// <summary>
/// Where the users stopped listening: the players save the queue, its current entry and position, so the user
/// continues on another device or after a reload. The most recently changed state wins.
/// </summary>
public interface IPlaybackStateService
{
    /// <summary>
    /// The saved state of the user with its tracks, or <c>null</c> if none was saved. Entries of tracks that left the
    /// library are left out.
    /// </summary>
    Task<PlaybackStateView?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the state of a device for the user, unless the saved state changed later (another device was faster).
    /// Unknown tracks are kept; they are left out when the state is read.
    /// </summary>
    /// <exception cref="WaveMP.Data.Repositories.UserNotFoundException">The user does not exist (any more).</exception>
    Task<PlaybackStateSaveStatus> SaveAsync(
        Guid userId,
        SavePlaybackStateCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets where the user stopped listening, so the clients start without a queue. A device that still plays
    /// saves its state again. Returns <c>false</c> if no state was saved.
    /// </summary>
    Task<bool> ClearAsync(Guid userId, CancellationToken cancellationToken = default);
}
