namespace WaveMP.Core.Users.Pins;

/// <summary>
/// Result of checking the PIN when a profile is selected.
/// </summary>
/// <param name="Status">The result.</param>
/// <param name="LockoutEnd">End of the lockout for <see cref="PinCheckStatus.LockedOut"/>, otherwise
/// <c>null</c>.</param>
public sealed record PinCheck(PinCheckStatus Status, DateTimeOffset? LockoutEnd = null);
