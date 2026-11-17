namespace WaveMP.Core.Users.Pins;

/// <summary>
/// Whether a profile is protected by a PIN.
/// </summary>
/// <param name="IsSet">The profile has a PIN.</param>
/// <param name="IsManagedByEnvironment">
/// The PIN comes from <c>ADMIN_PIN</c> (built-in admin profile only) and cannot be changed or removed.
/// </param>
public sealed record PinStatus(bool IsSet, bool IsManagedByEnvironment);
