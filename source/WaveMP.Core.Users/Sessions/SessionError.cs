namespace WaveMP.Core.Users.Sessions;

/// <summary>
/// Reasons why selecting a profile created no session.
/// </summary>
public enum SessionError
{
    /// <summary>
    /// The profile does not exist, also if it was deleted while the session was created.
    /// </summary>
    ProfileNotFound,

    /// <summary>
    /// The profile is protected by a PIN, but none was given.
    /// </summary>
    PinRequired,

    /// <summary>
    /// The PIN is wrong.
    /// </summary>
    PinInvalid,

    /// <summary>
    /// Too many wrong PINs: the profile is locked out until <see cref="SessionResult.LockoutEnd"/>.
    /// </summary>
    PinLockedOut,
}
