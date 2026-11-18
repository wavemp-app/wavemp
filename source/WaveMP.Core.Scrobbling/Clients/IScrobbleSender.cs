using WaveMP.Data.Entities;

namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// Talks to the scrobbling service of a connection: hands each request to the client of the service
/// (<see cref="ILastFmClient"/>, <see cref="IListenBrainzClient"/>).
/// </summary>
public interface IScrobbleSender
{
    /// <summary>
    /// Checks the token the user brought from the service: the token of the web authentication of Last.fm or the
    /// user token of ListenBrainz.
    /// </summary>
    Task<ScrobbleAuthorization> AuthorizeAsync(
        ScrobbleService service,
        string token,
        CancellationToken cancellationToken = default);

    Task<ScrobbleSendStatus> SendNowPlayingAsync(
        ScrobbleService service,
        string credential,
        ScrobbleTrack track,
        CancellationToken cancellationToken = default);

    Task<ScrobbleSendStatus> SendScrobblesAsync(
        ScrobbleService service,
        string credential,
        IReadOnlyList<ScrobblePlay> plays,
        CancellationToken cancellationToken = default);
}
