namespace WaveMP.Service.Dtos;

/// <summary>
/// The Subsonic access of the current profile: whether Subsonic apps may sign in and whether an app password exists.
/// The app password itself is only returned once, when it is created. Apps sign in with the name of the
/// profile and the app password at the address of the server.
/// </summary>
public sealed class SubsonicAccessDto
{
    /// <summary>
    /// True if Subsonic apps may sign in as the profile; off by default. While it is off, every sign-in fails.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// True if the profile has an app password.
    /// </summary>
    public required bool HasPassword { get; init; }

    /// <summary>
    /// When the app password was created; null without one.
    /// </summary>
    public DateTimeOffset? PasswordCreatedAt { get; init; }
}
