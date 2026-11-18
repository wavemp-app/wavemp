using System.Security.Cryptography;
using System.Text;
using WaveMP.Core.Subsonic.Accounts;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Core.Users.Profiles;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Authentication;

/// <summary>
/// The three ways of signing in: the OpenSubsonic API key (the app password, without user name), the token method
/// (<c>t</c> = md5(password + <c>s</c>)) and the password itself (<c>p</c>, also hex-encoded as <c>enc:...</c>). The
/// user name is the name of the profile, compared like the profile names (without regard to case). Passwords and
/// tokens are compared in constant time.
/// </summary>
internal sealed class SubsonicAuthenticator(
    ISubsonicRepository repository,
    ISubsonicPasswordProtector protector) : ISubsonicAuthenticator
{
    private const string EncodedPrefix = "enc:";

    public async Task<SubsonicAuthentication> AuthenticateAsync(
        SubsonicCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        bool hasApiKey = !string.IsNullOrEmpty(credentials.ApiKey);
        bool hasPassword = credentials.Password is not null;
        bool hasToken = credentials.Token is not null || credentials.Salt is not null;
        if (hasApiKey)
        {
            if (credentials.UserName is not null || hasPassword || hasToken)
            {
                return SubsonicAuthentication.Failure(SubsonicErrorCode.ConflictingAuthentication);
            }

            return await AuthenticateApiKeyAsync(credentials.ApiKey!, cancellationToken);
        }

        if (string.IsNullOrEmpty(credentials.UserName))
        {
            return SubsonicAuthentication.Failure(SubsonicErrorCode.MissingParameter);
        }

        if (hasPassword && hasToken)
        {
            return SubsonicAuthentication.Failure(SubsonicErrorCode.ConflictingAuthentication);
        }

        if (!hasPassword && (string.IsNullOrEmpty(credentials.Token) || string.IsNullOrEmpty(credentials.Salt)))
        {
            return SubsonicAuthentication.Failure(SubsonicErrorCode.MissingParameter);
        }

        if (!ProfileNames.TryNormalize(credentials.UserName, out string name))
        {
            return SubsonicAuthentication.Failure(SubsonicErrorCode.WrongCredentials);
        }

        SubsonicLogin? login = await repository.FindLoginByNameAsync(
            ProfileNames.ToNormalized(name),
            cancellationToken);
        if (login is not { Enabled: true, ProtectedPassword: { } protectedPassword }
            || protector.Unprotect(protectedPassword) is not { } password)
        {
            return SubsonicAuthentication.Failure(SubsonicErrorCode.WrongCredentials);
        }

        bool matches = hasPassword
            ? PasswordMatches(password, credentials.Password!)
            : TokenMatches(password, credentials.Salt!, credentials.Token!);
        return matches
            ? SubsonicAuthentication.Success(new SubsonicUser(login.UserId, login.UserName))
            : SubsonicAuthentication.Failure(SubsonicErrorCode.WrongCredentials);
    }

    /// <summary>
    /// md5(password + salt) as lower-case hex, as the token method of the Subsonic API defines it.
    /// </summary>
    public static string CreateToken(string password, string salt)
    {
        return Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(password + salt)));
    }

    private async Task<SubsonicAuthentication> AuthenticateApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken)
    {
        SubsonicLogin? login = await repository.FindLoginByPasswordHashAsync(
            SubsonicPasswords.Hash(apiKey),
            cancellationToken);
        if (login is not { Enabled: true, ProtectedPassword: { } protectedPassword }
            || protector.Unprotect(protectedPassword) is not { } password
            || !Equal(password, apiKey))
        {
            return SubsonicAuthentication.Failure(SubsonicErrorCode.InvalidApiKey);
        }

        return SubsonicAuthentication.Success(new SubsonicUser(login.UserId, login.UserName));
    }

    private static bool PasswordMatches(string password, string sent)
    {
        if (sent.StartsWith(EncodedPrefix, StringComparison.Ordinal))
        {
            try
            {
                sent = Encoding.UTF8.GetString(Convert.FromHexString(sent[EncodedPrefix.Length..]));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        return Equal(password, sent);
    }

    private static bool TokenMatches(string password, string salt, string token)
    {
        return Equal(CreateToken(password, salt), token.Trim().ToLowerInvariant());
    }

    private static bool Equal(string expected, string actual)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
    }
}
