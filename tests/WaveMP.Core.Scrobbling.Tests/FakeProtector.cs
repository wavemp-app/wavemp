using WaveMP.Core.Scrobbling.Credentials;

namespace WaveMP.Core.Scrobbling.Tests;

/// <summary>
/// "Encrypts" by a prefix, so tests can see that only protected credentials are stored.
/// </summary>
internal sealed class FakeProtector : IScrobbleCredentialProtector
{
    public const string Prefix = "protected:";

    public string Protect(string credential)
    {
        return Prefix + credential;
    }

    public string? Unprotect(string protectedCredential)
    {
        return protectedCredential.StartsWith(Prefix, StringComparison.Ordinal)
            ? protectedCredential[Prefix.Length..]
            : null;
    }
}
