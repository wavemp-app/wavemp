using Microsoft.Extensions.Options;
using WaveMP.Core.Users.Pins;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Users.Sessions;

internal sealed class SessionService(
    IUserRepository users,
    ISessionRepository sessions,
    IPinService pins,
    TimeProvider timeProvider,
    IOptions<SessionOptions> options) : ISessionService
{
    public async Task<SessionResult> CreateAsync(
        Guid profileId,
        string? pin = null,
        CancellationToken cancellationToken = default)
    {
        User? user = await users.FindAsync(profileId, cancellationToken);
        if (user is null)
        {
            return SessionResult.Failure(SessionError.ProfileNotFound);
        }

        PinCheck check = await pins.CheckAsync(user, pin, cancellationToken);
        switch (check.Status)
        {
            case PinCheckStatus.Passed:
                break;
            case PinCheckStatus.Required:
                return SessionResult.Failure(SessionError.PinRequired);
            case PinCheckStatus.Invalid:
                return SessionResult.Failure(SessionError.PinInvalid);
            case PinCheckStatus.LockedOut:
                return SessionResult.Failure(SessionError.PinLockedOut, check.LockoutEnd);
            case PinCheckStatus.NotFound:
                return SessionResult.Failure(SessionError.ProfileNotFound);
            default:
                throw new InvalidOperationException($"Unknown result of the PIN check: {check.Status}.");
        }

        // Expired sessions are removed here instead of in a background job.
        DateTimeOffset now = timeProvider.GetUtcNow();
        await sessions.DeleteExpiredAsync(now, cancellationToken);

        Session session = new()
        {
            Id = Guid.CreateVersion7(now),
            UserId = user.Id,
            CreatedAt = now,
            ExpiresAt = now + options.Value.Lifetime,
        };
        try
        {
            await sessions.AddAsync(session, cancellationToken);
        }
        catch (UserNotFoundException)
        {
            // The profile was deleted in the meantime.
            return SessionResult.Failure(SessionError.ProfileNotFound);
        }

        return SessionResult.Success(new ActiveSession(session, user));
    }

    public async Task<ActiveSession?> GetActiveAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        Session? session = await sessions.FindActiveAsync(sessionId, timeProvider.GetUtcNow(), cancellationToken);
        return session?.User is { } user ? new ActiveSession(session, user) : null;
    }

    public Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return sessions.DeleteAsync(sessionId, cancellationToken);
    }
}
