using WaveMP.Core.Subsonic.Accounts;

namespace WaveMP.Core.Subsonic.Tests;

/// <summary>
/// "Encrypts" by a prefix, so tests can see that only protected passwords are stored.
/// </summary>
internal sealed class FakeProtector : ISubsonicPasswordProtector
{
    public const string Prefix = "protected:";

    public string Protect(string password)
    {
        return Prefix + password;
    }

    public string? Unprotect(string protectedPassword)
    {
        return protectedPassword.StartsWith(Prefix, StringComparison.Ordinal)
            ? protectedPassword[Prefix.Length..]
            : null;
    }
}
