using Microsoft.Extensions.Options;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Users.Pins;

internal sealed class PinService(
    IUserRepository users,
    ISessionRepository sessions,
    IPinHasher hasher,
    TimeProvider timeProvider,
    IOptions<PinOptions> options) : IPinService
{
    public async Task<PinStatus?> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        User? user = await users.FindAsync(userId, cancellationToken);
        return user is null ? null : new PinStatus(user.PinHash is not null, IsManagedByEnvironment(user));
    }

    public bool IsManagedByEnvironment(User user)
    {
        return user.IsBuiltIn && options.Value.HasAdminPin;
    }

    public async Task<PinCheck> CheckAsync(User user, string? pin, CancellationToken cancellationToken = default)
    {
        if (user.PinHash is null)
        {
            return new PinCheck(PinCheckStatus.Passed);
        }

        if (string.IsNullOrEmpty(pin))
        {
            return new PinCheck(PinCheckStatus.Required);
        }

        PinOptions settings = options.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();

        // Counted before the check, so parallel attempts cannot get past the limit.
        PinAttempt? attempt = await users.BeginPinAttemptAsync(
            user.Id,
            now,
            settings.MaxFailedAttempts,
            now + settings.LockoutDuration,
            cancellationToken);
        if (attempt is null)
        {
            return new PinCheck(PinCheckStatus.NotFound);
        }

        if (!attempt.IsAllowed)
        {
            return new PinCheck(PinCheckStatus.LockedOut, attempt.LockoutEnd);
        }

        if (PinRules.IsValid(pin) && hasher.Verify(user.PinHash, pin))
        {
            await users.ResetPinAttemptsAsync(user.Id, cancellationToken);
            return new PinCheck(PinCheckStatus.Passed);
        }

        // The wrong attempt that reached the limit started the lockout.
        return attempt.LockoutEnd is { } lockoutEnd && lockoutEnd > now
            ? new PinCheck(PinCheckStatus.LockedOut, lockoutEnd)
            : new PinCheck(PinCheckStatus.Invalid);
    }

    public async Task<PinChangeStatus> SetAsync(
        Guid userId,
        string pin,
        Guid? keepSessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (!PinRules.IsValid(pin))
        {
            return PinChangeStatus.InvalidPin;
        }

        User? user = await users.FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return PinChangeStatus.NotFound;
        }

        if (IsManagedByEnvironment(user))
        {
            return PinChangeStatus.ManagedByEnvironment;
        }

        if (!await users.SetPinHashAsync(userId, hasher.Hash(pin), cancellationToken))
        {
            return PinChangeStatus.NotFound;
        }

        // Devices that selected the profile before have to enter the new PIN.
        await sessions.DeleteForUserAsync(userId, keepSessionId, cancellationToken);
        return PinChangeStatus.Success;
    }

    public async Task<PinChangeStatus> RemoveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        User? user = await users.FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return PinChangeStatus.NotFound;
        }

        if (IsManagedByEnvironment(user))
        {
            return PinChangeStatus.ManagedByEnvironment;
        }

        return await users.SetPinHashAsync(userId, null, cancellationToken)
            ? PinChangeStatus.Success
            : PinChangeStatus.NotFound;
    }
}
