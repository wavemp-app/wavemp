namespace WaveMP.Core.Users.Pins;

/// <summary>
/// Settings of the PINs (configuration section <c>Pins</c>) and the PIN of the built-in admin profile from the
/// environment variable <c>ADMIN_PIN</c>.
/// </summary>
public sealed class PinOptions
{
    public const string SectionName = "Pins";

    /// <summary>
    /// Environment variable with the PIN of the built-in admin profile.
    /// </summary>
    public const string AdminPinVariable = "ADMIN_PIN";

    /// <summary>
    /// Wrong PINs in a row after which a profile is locked out for <see cref="LockoutDuration"/>.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// How long a profile stays locked out after too many wrong PINs.
    /// </summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// PIN of the built-in admin profile from <c>ADMIN_PIN</c>; <c>null</c> if the variable is not set or empty.
    /// While it is set, it is applied on every start and the PIN of the admin profile cannot be changed otherwise.
    /// </summary>
    public string? AdminPin { get; set; }

    public bool HasAdminPin => !string.IsNullOrEmpty(AdminPin);
}
