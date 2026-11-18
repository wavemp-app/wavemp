using System.Diagnostics.CodeAnalysis;
using WaveMP.Core.Subsonic.Responses;

namespace WaveMP.Core.Subsonic.Authentication;

/// <summary>
/// The user a request signed in as, or the error code of the failed sign-in.
/// </summary>
public sealed record SubsonicAuthentication(SubsonicUser? User, SubsonicErrorCode? Error)
{
    [MemberNotNullWhen(true, nameof(User))]
    public bool IsAuthenticated => User is not null;

    public static SubsonicAuthentication Success(SubsonicUser user)
    {
        return new SubsonicAuthentication(user, null);
    }

    public static SubsonicAuthentication Failure(SubsonicErrorCode error)
    {
        return new SubsonicAuthentication(null, error);
    }
}
