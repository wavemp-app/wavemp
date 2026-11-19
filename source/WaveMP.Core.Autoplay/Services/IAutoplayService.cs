using WaveMP.Data.Projections;

namespace WaveMP.Core.Autoplay.Services;

/// <summary>
/// Chooses the songs that play when a list ends while autoplay is on, following the settings of the user.
/// </summary>
public interface IAutoplayService
{
    /// <summary>
    /// The next songs with their artist and album and the favorite and rating of the user, in the order they play;
    /// empty if no matching song is found. <c>null</c> if the user does not exist.
    /// </summary>
    Task<IReadOnlyList<TrackRow>?> SelectAsync(
        Guid userId,
        AutoplayCommand command,
        CancellationToken cancellationToken = default);
}
