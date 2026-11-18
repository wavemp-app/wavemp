namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// The API of ListenBrainz (version 1) for listens. Users authorize with their personal user token from the settings
/// of ListenBrainz. Never throws for network errors; the answer says what happened.
/// </summary>
public interface IListenBrainzClient
{
    /// <summary>
    /// Checks the user token and reads the name of its account (<c>GET 1/validate-token</c>). The token itself is the
    /// credential to keep.
    /// </summary>
    Task<ScrobbleAuthorization> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports the song the user starts to listen to (<c>POST 1/submit-listens</c>, type <c>playing_now</c>).
    /// </summary>
    Task<ScrobbleSendStatus> SubmitPlayingNowAsync(
        string token,
        ScrobbleTrack track,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits plays (<c>POST 1/submit-listens</c>, type <c>single</c> for one play, <c>import</c> for several).
    /// </summary>
    Task<ScrobbleSendStatus> SubmitListensAsync(
        string token,
        IReadOnlyList<ScrobblePlay> plays,
        CancellationToken cancellationToken = default);
}
