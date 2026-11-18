namespace WaveMP.Core.Subsonic.Accounts;

/// <summary>
/// The Subsonic access of a user as the settings show it; the app password itself is never shown again.
/// </summary>
/// <param name="Enabled">Whether Subsonic apps may sign in as the user.</param>
/// <param name="HasPassword">Whether the user has an app password.</param>
/// <param name="PasswordCreatedAt">When the app password was created.</param>
public sealed record SubsonicAccountState(bool Enabled, bool HasPassword, DateTimeOffset? PasswordCreatedAt)
{
    public static SubsonicAccountState Off { get; } = new(false, false, null);
}
