namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// The API of Last.fm (version 2.0) for scrobbling. Every request is signed with the shared secret of the API account
/// of WaveMP (see <see cref="Options.ScrobblingOptions"/>). Never throws for network errors; the answer says what
/// happened.
/// </summary>
public interface ILastFmClient
{
    /// <summary>
    /// Exchanges the token of the web authentication, which Last.fm hands to the callback URL once the user allowed
    /// access, for a session key (<c>auth.getSession</c>). The session key does not expire.
    /// </summary>
    Task<ScrobbleAuthorization> GetSessionAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports the song the user starts to listen to (<c>track.updateNowPlaying</c>).
    /// </summary>
    Task<ScrobbleSendStatus> UpdateNowPlayingAsync(
        string sessionKey,
        ScrobbleTrack track,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scrobbles up to 50 plays at once (<c>track.scrobble</c>).
    /// </summary>
    Task<ScrobbleSendStatus> ScrobbleAsync(
        string sessionKey,
        IReadOnlyList<ScrobblePlay> plays,
        CancellationToken cancellationToken = default);
}
