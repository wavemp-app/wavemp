using System.Diagnostics.CodeAnalysis;

namespace WaveMP.Core.Users.Sessions;

/// <summary>
/// Result of selecting a profile: the new session or the reason why none was created.
/// </summary>
/// <param name="Session">The new session.</param>
/// <param name="Error">Why no session was created.</param>
/// <param name="LockoutEnd">End of the lockout for <see cref="SessionError.PinLockedOut"/>.</param>
public sealed record SessionResult(ActiveSession? Session, SessionError? Error, DateTimeOffset? LockoutEnd = null)
{
    [MemberNotNullWhen(true, nameof(Session))]
    public bool IsSuccess => Session is not null;

    public static SessionResult Success(ActiveSession session)
    {
        return new SessionResult(session, null);
    }

    public static SessionResult Failure(SessionError error, DateTimeOffset? lockoutEnd = null)
    {
        return new SessionResult(null, error, lockoutEnd);
    }
}
