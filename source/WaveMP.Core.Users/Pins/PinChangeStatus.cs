namespace WaveMP.Core.Users.Pins;

/// <summary>
/// Result of setting or removing a PIN.
/// </summary>
public enum PinChangeStatus
{
    Success,

    /// <summary>
    /// The profile does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// The PIN does not consist of 4 to 8 digits.
    /// </summary>
    InvalidPin,

    /// <summary>
    /// The PIN of the built-in admin profile comes from <c>ADMIN_PIN</c> and cannot be changed or removed.
    /// </summary>
    ManagedByEnvironment,
}
