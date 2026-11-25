namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// A live connection of a client, authenticated with a session of a profile.
/// </summary>
/// <param name="UserId">The profile of the session; the connection only sees and controls its devices.</param>
/// <param name="SessionId">The session the connection was opened with; it is closed when the session ends.</param>
/// <param name="ConnectionId">The id of the connection, unique on this server.</param>
public sealed record DeviceConnection(Guid UserId, Guid SessionId, string ConnectionId);
