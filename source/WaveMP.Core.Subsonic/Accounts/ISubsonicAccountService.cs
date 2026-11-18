namespace WaveMP.Core.Subsonic.Accounts;

/// <summary>
/// The Subsonic access of the users: switched on or off (off by default) and the app password.
/// </summary>
public interface ISubsonicAccountService
{
    Task<SubsonicAccountState> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <exception cref="WaveMP.Data.Repositories.UserNotFoundException">The user does not exist.</exception>
    Task<SubsonicAccountState> SetEnabledAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new app password, which replaces an earlier one at once. Only its encrypted form and its hash are
    /// stored.
    /// </summary>
    /// <exception cref="WaveMP.Data.Repositories.UserNotFoundException">The user does not exist.</exception>
    Task<SubsonicPassword> CreatePasswordAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the app password; Subsonic apps can no longer sign in until a new one is created.
    /// </summary>
    Task<SubsonicAccountState> RevokePasswordAsync(Guid userId, CancellationToken cancellationToken = default);
}
