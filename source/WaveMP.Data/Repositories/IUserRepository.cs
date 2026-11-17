using WaveMP.Data.Entities;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Access to the users and their settings.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Lists the users sorted and filtered as requested; ties are sorted by id.
    /// </summary>
    Task<Page<User>> ListAsync(UserListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the profiles for the profile selection: in the order they were created, the built-in admin last.
    /// </summary>
    Task<Page<User>> ListProfilesAsync(int offset, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a user with its settings. The user is tracked, so changes can be saved with <see cref="UpdateAsync"/>.
    /// </summary>
    Task<User?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a user other than <paramref name="exceptId"/> has the normalized name.
    /// </summary>
    Task<bool> NameExistsAsync(
        string normalizedName,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default);

    Task<bool> BuiltInAdminExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the built-in admin profile; nothing is tracked.
    /// </summary>
    Task<User?> FindBuiltInAdminAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Colors of all users, one entry per user.
    /// </summary>
    Task<IReadOnlyList<string>> ListColorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a user with its settings.
    /// </summary>
    /// <exception cref="UserNameConflictException">Another user has the same name.</exception>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the changes of a user loaded with <see cref="FindAsync"/>.
    /// </summary>
    /// <exception cref="UserNameConflictException">Another user has the same name.</exception>
    /// <exception cref="UserNotFoundException">The user was deleted since it was loaded.</exception>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a PIN attempt of the user before the PIN is checked, in one atomic statement, so parallel attempts
    /// cannot get past the limit. Nothing is counted while the user is locked out (<see cref="User.PinLockoutEnd"/> is
    /// after <paramref name="now"/>). After a lockout has ended, counting starts again. The attempt that reaches
    /// <paramref name="maxFailedAttempts"/> starts the lockout until <paramref name="lockoutEnd"/>; a correct PIN
    /// lifts it with <see cref="ResetPinAttemptsAsync"/>. Returns <c>null</c> if the user does not exist.
    /// </summary>
    Task<PinAttempt?> BeginPinAttemptAsync(
        Guid userId,
        DateTimeOffset now,
        int maxFailedAttempts,
        DateTimeOffset lockoutEnd,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the counted PIN attempts and lifts a lockout, e.g. after a correct PIN.
    /// </summary>
    Task ResetPinAttemptsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets (or with <c>null</c> removes) the hash of the PIN of the user, forgets the counted attempts and lifts a
    /// lockout. Returns <c>false</c> if the user does not exist.
    /// </summary>
    Task<bool> SetPinHashAsync(Guid userId, string? pinHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// The users among <paramref name="ids"/> that exist and have an uploaded avatar (an avatar that is not one of the
    /// built-in ones).
    /// </summary>
    Task<IReadOnlySet<Guid>> ListUploadedAvatarIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user loaded with <see cref="FindAsync"/>, together with its settings and sessions.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user was deleted since it was loaded.</exception>
    Task DeleteAsync(User user, CancellationToken cancellationToken = default);
}
