using System.Security.Cryptography;
using System.Text;

namespace WaveMP.Core.Subsonic.Accounts;

/// <summary>
/// The app passwords of the Subsonic API: random, long enough to need no further protection against guessing, and
/// easy to type into a phone (letters and digits without the ones that look alike).
/// </summary>
public static class SubsonicPasswords
{
    /// <summary>
    /// Number of characters of an app password; with 56 possible characters about 139 bits of chance.
    /// </summary>
    public const int Length = 24;

    private const string Alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        return RandomNumberGenerator.GetString(Alphabet, Length);
    }

    /// <summary>
    /// SHA-256 of the password as lower-case hex: finds the user of an API key. The password is random and long, so a
    /// fast hash is enough.
    /// </summary>
    public static string Hash(string password)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }
}
