namespace WaveMP.Service.Dtos;

/// <summary>
/// Whether a profile is protected by a PIN. The PIN itself is never returned.
/// </summary>
public sealed class PinStatusDto
{
    /// <summary>
    /// True if the profile can only be selected with its PIN.
    /// </summary>
    public required bool IsSet { get; init; }

    /// <summary>
    /// True if the PIN is set by the environment variable ADMIN_PIN (built-in admin profile only); it cannot be
    /// changed or removed then.
    /// </summary>
    public required bool IsManagedByEnvironment { get; init; }
}
