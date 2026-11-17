namespace WaveMP.Data.Repositories;

/// <summary>
/// A PIN attempt as counted by <see cref="IUserRepository.BeginPinAttemptAsync"/>.
/// </summary>
/// <param name="IsAllowed">
/// <c>true</c> if the attempt was counted and the PIN may be checked; <c>false</c> while the profile is locked out.
/// </param>
/// <param name="FailedAttempts">Attempts counted since the last correct PIN or the end of the last lockout.</param>
/// <param name="LockoutEnd">
/// End of the lockout: the current one if the attempt was not allowed, or the one this attempt started if it was the
/// last one before the limit (it is lifted again if the PIN turns out to be correct); otherwise <c>null</c>.
/// </param>
public sealed record PinAttempt(bool IsAllowed, int FailedAttempts, DateTimeOffset? LockoutEnd);
