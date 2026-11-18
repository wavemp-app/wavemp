namespace WaveMP.Data.Entities;

/// <summary>
/// The access of a user to the Subsonic API, at most one per user: whether it is switched on and the app password
/// that Subsonic apps sign in with. Removed with its user. A user without a row has the access switched off.
/// </summary>
public class SubsonicAccount
{
    public required Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// Whether Subsonic apps may sign in as the user; off by default. While it is off, every sign-in fails.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The app password, encrypted with ASP.NET Core Data Protection; never stored in plain text. The token method of
    /// Subsonic (<c>md5(password + salt)</c>) needs the password itself on the server, so a hash is not enough.
    /// <c>null</c> without an app password.
    /// </summary>
    public string? ProtectedPassword { get; set; }

    /// <summary>
    /// SHA-256 of the app password as lower-case hex; finds the user of an OpenSubsonic API key, which is the app
    /// password as well. <c>null</c> without an app password.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// When the app password was created; <c>null</c> without an app password.
    /// </summary>
    public DateTimeOffset? PasswordCreatedAt { get; set; }
}
