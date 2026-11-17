namespace WaveMP.Core.Users.Sessions;

/// <summary>
/// Creates, finds and ends the sessions of the profiles.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Selects a profile: creates a session for it. A profile protected by a PIN needs its PIN; wrong PINs are counted
    /// and lock the profile out for a while (see <see cref="Pins.IPinService.CheckAsync"/>). Fails with
    /// <see cref="SessionError.ProfileNotFound"/> if the profile does not exist, also if it was deleted while the
    /// session was created. Expired sessions of all profiles are removed on the way.
    /// </summary>
    Task<SessionResult> CreateAsync(
        Guid profileId,
        string? pin = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a session that has not expired, or <c>null</c>.
    /// </summary>
    Task<ActiveSession?> GetActiveAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends a session; its token is no longer accepted.
    /// </summary>
    Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
