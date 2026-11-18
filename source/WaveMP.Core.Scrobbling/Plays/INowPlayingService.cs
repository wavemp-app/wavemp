namespace WaveMP.Core.Scrobbling.Plays;

/// <summary>
/// Reports the song a user starts to listen to as "Now Playing" to every connected service. "Now Playing" is no
/// scrobble: it is sent once, right away, and not queued.
/// </summary>
public interface INowPlayingService
{
    /// <summary>
    /// Reports the track to the active connections of the user, unless it is shorter than 30 seconds or has no
    /// artist. Returns <see cref="NowPlayingStatus.TrackNotFound"/> if the track does not exist.
    /// </summary>
    Task<NowPlayingStatus> ReportAsync(Guid userId, Guid trackId, CancellationToken cancellationToken = default);
}
