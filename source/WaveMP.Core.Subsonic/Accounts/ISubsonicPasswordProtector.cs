namespace WaveMP.Core.Subsonic.Accounts;

/// <summary>
/// Encrypts the app passwords before they are stored, with ASP.NET Core Data Protection (keys in the folder
/// <c>keys</c> of the data folder).
/// </summary>
public interface ISubsonicPasswordProtector
{
    string Protect(string password);

    /// <summary>
    /// The password, or <c>null</c> if it cannot be decrypted (e.g. the keys of the data folder were lost).
    /// </summary>
    string? Unprotect(string protectedPassword);
}
