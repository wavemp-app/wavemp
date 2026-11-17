namespace WaveMP.Core.Users.Pins;

/// <summary>
/// Hashes PINs and checks them against a hash. PINs are never stored in plain text.
/// </summary>
public interface IPinHasher
{
    /// <summary>
    /// A salted hash of the PIN; hashing the same PIN twice gives different hashes.
    /// </summary>
    string Hash(string pin);

    /// <summary>
    /// Checks a PIN against a hash created by <see cref="Hash"/>. A damaged hash matches no PIN.
    /// </summary>
    bool Verify(string hash, string pin);
}
