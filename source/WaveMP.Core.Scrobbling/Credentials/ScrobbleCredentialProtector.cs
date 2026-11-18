using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace WaveMP.Core.Scrobbling.Credentials;

internal sealed class ScrobbleCredentialProtector(IDataProtectionProvider provider) : IScrobbleCredentialProtector
{
    /// <summary>
    /// Purpose of the protector; changing it makes all stored credentials unreadable.
    /// </summary>
    public const string Purpose = "WaveMP.Scrobbling.Credentials.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string credential)
    {
        return _protector.Protect(credential);
    }

    public string? Unprotect(string protectedCredential)
    {
        try
        {
            return _protector.Unprotect(protectedCredential);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
