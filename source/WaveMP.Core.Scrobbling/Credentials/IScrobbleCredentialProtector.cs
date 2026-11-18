namespace WaveMP.Core.Scrobbling.Credentials;

/// <summary>
/// Encrypts the credentials of the scrobbling services before they are stored, with ASP.NET Core Data Protection
/// (keys in the folder <c>keys</c> of the data folder).
/// </summary>
public interface IScrobbleCredentialProtector
{
    string Protect(string credential);

    /// <summary>
    /// The credential, or <c>null</c> if it cannot be decrypted (e.g. the keys of the data folder were lost).
    /// </summary>
    string? Unprotect(string protectedCredential);
}
