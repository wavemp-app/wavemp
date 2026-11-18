using WaveMP.Data.Entities;

namespace WaveMP.Core.Scrobbling.Connections;

/// <summary>
/// The state of a scrobbling service for a user.
/// </summary>
/// <param name="Service">The service.</param>
/// <param name="IsAvailable">Whether the service can be connected; Last.fm needs the API account of WaveMP.</param>
/// <param name="Connection">The connection of the user, or <c>null</c> if not connected.</param>
/// <param name="QueuedCount">Scrobbles of the user that wait to be sent to the service.</param>
/// <param name="AuthorizationUrl">Page of the service where the user allows WaveMP to scrobble (Last.fm only; the
/// client adds its callback URL as parameter <c>cb</c>), or <c>null</c>.</param>
public sealed record ScrobbleServiceState(
    ScrobbleService Service,
    bool IsAvailable,
    ScrobbleConnection? Connection,
    int QueuedCount,
    Uri? AuthorizationUrl);
