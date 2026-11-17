using WaveMP.Data.Entities;

namespace WaveMP.Core.Users.Pins;

/// <summary>
/// The optional PINs of the profiles: checks them when a profile is selected, counts wrong attempts with a temporary
/// lockout, and sets and removes them.
/// </summary>
public interface IPinService
{
    /// <summary>
    /// Whether the profile has a PIN and whether it comes from <c>ADMIN_PIN</c>; <c>null</c> if the profile does not
    /// exist.
    /// </summary>
    Task<PinStatus?> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True if the PIN of the user comes from <c>ADMIN_PIN</c> and cannot be changed or removed.
    /// </summary>
    bool IsManagedByEnvironment(User user);

    /// <summary>
    /// Checks the PIN of a profile that is being selected. A profile without a PIN always passes. Every given PIN is
    /// counted as an attempt before it is checked; a correct PIN resets the count, too many wrong ones lock the
    /// profile out for a while (<see cref="PinOptions"/>).
    /// </summary>
    Task<PinCheck> CheckAsync(User user, string? pin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or changes the PIN of a profile. All sessions of the profile except <paramref name="keepSessionId"/> end,
    /// so devices that selected the profile before have to enter the PIN.
    /// </summary>
    Task<PinChangeStatus> SetAsync(
        Guid userId,
        string pin,
        Guid? keepSessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the PIN of a profile and lifts a lockout. The sessions of the profile stay.
    /// </summary>
    Task<PinChangeStatus> RemoveAsync(Guid userId, CancellationToken cancellationToken = default);
}
