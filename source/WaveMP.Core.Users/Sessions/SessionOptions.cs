namespace WaveMP.Core.Users.Sessions;

/// <summary>
/// Settings of the sessions created by selecting a profile.
/// </summary>
public sealed class SessionOptions
{
    /// <summary>
    /// How long a session and its token stay valid. Profiles have no password (at most a PIN), so the selection is
    /// remembered until the profile is switched; setting a PIN ends the other sessions of the profile.
    /// </summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(365);
}
