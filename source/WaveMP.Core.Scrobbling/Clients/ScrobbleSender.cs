using WaveMP.Data.Entities;

namespace WaveMP.Core.Scrobbling.Clients;

internal sealed class ScrobbleSender(ILastFmClient lastFm, IListenBrainzClient listenBrainz) : IScrobbleSender
{
    public Task<ScrobbleAuthorization> AuthorizeAsync(
        ScrobbleService service,
        string token,
        CancellationToken cancellationToken = default)
    {
        return service switch
        {
            ScrobbleService.LastFm => lastFm.GetSessionAsync(token, cancellationToken),
            ScrobbleService.ListenBrainz => listenBrainz.ValidateTokenAsync(token, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown scrobbling service."),
        };
    }

    public Task<ScrobbleSendStatus> SendNowPlayingAsync(
        ScrobbleService service,
        string credential,
        ScrobbleTrack track,
        CancellationToken cancellationToken = default)
    {
        return service switch
        {
            ScrobbleService.LastFm => lastFm.UpdateNowPlayingAsync(credential, track, cancellationToken),
            ScrobbleService.ListenBrainz => listenBrainz.SubmitPlayingNowAsync(credential, track, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown scrobbling service."),
        };
    }

    public Task<ScrobbleSendStatus> SendScrobblesAsync(
        ScrobbleService service,
        string credential,
        IReadOnlyList<ScrobblePlay> plays,
        CancellationToken cancellationToken = default)
    {
        return service switch
        {
            ScrobbleService.LastFm => lastFm.ScrobbleAsync(credential, plays, cancellationToken),
            ScrobbleService.ListenBrainz => listenBrainz.SubmitListensAsync(credential, plays, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown scrobbling service."),
        };
    }
}
