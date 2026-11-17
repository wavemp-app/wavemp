using Microsoft.AspNetCore.Identity;

namespace WaveMP.Core.Users.Pins;

/// <summary>
/// Hashes PINs with the <see cref="PasswordHasher{TUser}"/> of ASP.NET Core Identity: PBKDF2 with HMAC-SHA512, a
/// random salt per hash and many iterations, so the database does not reveal the PINs at a glance.
/// </summary>
internal sealed class PinHasher : IPinHasher
{
    private readonly PasswordHasher<PinHasher> _hasher = new();

    public string Hash(string pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        return _hasher.HashPassword(this, pin);
    }

    public bool Verify(string hash, string pin)
    {
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(pin);
        try
        {
            // A hash that needs a rehash (older parameters) still matches; WaveMP only creates current hashes.
            return _hasher.VerifyHashedPassword(this, hash, pin) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Not a hash of the hasher (no Base64), e.g. a damaged value.
            return false;
        }
    }
}
