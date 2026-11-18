using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace WaveMP.Core.Subsonic.Accounts;

internal sealed class SubsonicPasswordProtector(IDataProtectionProvider provider) : ISubsonicPasswordProtector
{
    /// <summary>
    /// Purpose of the protector; changing it makes all stored app passwords unreadable.
    /// </summary>
    public const string Purpose = "WaveMP.Subsonic.Passwords.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string password)
    {
        return _protector.Protect(password);
    }

    public string? Unprotect(string protectedPassword)
    {
        try
        {
            return _protector.Unprotect(protectedPassword);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
