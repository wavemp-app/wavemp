namespace WaveMP.Core.Users.Pins;

/// <summary>
/// Result of checking the PIN when a profile is selected.
/// </summary>
public enum PinCheckStatus
{
    /// <summary>
    /// The profile has no PIN, or the PIN is correct.
    /// </summary>
    Passed,

    /// <summary>
    /// The profile has a PIN, but none was given. Not counted as a wrong attempt.
    /// </summary>
    Required,

    /// <summary>
    /// The PIN is wrong.
    /// </summary>
    Invalid,

    /// <summary>
    /// Too many wrong PINs: the profile is locked out until <see cref="PinCheck.LockoutEnd"/>. Also the answer to the
    /// wrong attempt that starts the lockout.
    /// </summary>
    LockedOut,

    /// <summary>
    /// The profile was deleted in the meantime.
    /// </summary>
    NotFound,
}
