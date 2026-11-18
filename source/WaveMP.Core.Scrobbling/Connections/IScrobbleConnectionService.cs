using WaveMP.Data.Entities;

namespace WaveMP.Core.Scrobbling.Connections;

/// <summary>
/// Connects the profiles to their accounts at the scrobbling services: Last.fm through its web authentication,
/// ListenBrainz with the personal user token. The credentials are stored encrypted.
/// </summary>
public interface IScrobbleConnectionService
{
    /// <summary>
    /// The state of every service for the user, in the order of <see cref="ScrobbleService"/>.
    /// </summary>
    Task<IReadOnlyList<ScrobbleServiceState>> GetStatesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects the service with the token the user brought from it (Last.fm: the token of the web authentication,
    /// ListenBrainz: the user token) and replaces an earlier connection. Queued scrobbles of the service are sent
    /// again from now on.
    /// </summary>
    /// <exception cref="WaveMP.Data.Repositories.UserNotFoundException">The user does not exist (any more).</exception>
    Task<ScrobbleConnectResult> ConnectAsync(
        Guid userId,
        ScrobbleService service,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the connection to the service and its queued scrobbles. Returns <c>false</c> if it was not connected.
    /// </summary>
    Task<bool> DisconnectAsync(Guid userId, ScrobbleService service, CancellationToken cancellationToken = default);
}
