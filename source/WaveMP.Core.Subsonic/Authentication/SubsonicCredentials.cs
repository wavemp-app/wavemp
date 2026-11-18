using WaveMP.Core.Subsonic.Requests;

namespace WaveMP.Core.Subsonic.Authentication;

/// <summary>
/// What a Subsonic app sends to sign in: the user name with the password (<c>p</c>, plain or <c>enc:</c> hex) or with
/// token and salt (<c>t</c> = md5(password + salt), <c>s</c>), or an OpenSubsonic API key alone.
/// </summary>
public sealed record SubsonicCredentials(
    string? UserName,
    string? Password,
    string? Token,
    string? Salt,
    string? ApiKey)
{
    public static SubsonicCredentials From(SubsonicParameters parameters)
    {
        return new SubsonicCredentials(
            parameters.Get("u"),
            parameters.Get("p"),
            parameters.Get("t"),
            parameters.Get("s"),
            parameters.Get("apiKey"));
    }
}
