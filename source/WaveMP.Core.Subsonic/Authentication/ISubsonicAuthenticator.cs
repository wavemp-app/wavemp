namespace WaveMP.Core.Subsonic.Authentication;

/// <summary>
/// Signs in the requests of Subsonic apps with the app password of a profile.
/// </summary>
public interface ISubsonicAuthenticator
{
    /// <summary>
    /// Checks the credentials. Fails for users whose Subsonic access is off or who have no app password, in the same
    /// way as for a wrong password.
    /// </summary>
    Task<SubsonicAuthentication> AuthenticateAsync(
        SubsonicCredentials credentials,
        CancellationToken cancellationToken = default);
}
