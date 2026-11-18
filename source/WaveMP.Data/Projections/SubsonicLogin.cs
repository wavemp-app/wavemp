namespace WaveMP.Data.Projections;

/// <summary>
/// What a sign-in of a Subsonic app needs to know about a user.
/// </summary>
/// <param name="UserId">Id of the user.</param>
/// <param name="UserName">Name of the profile, the user name of the Subsonic apps.</param>
/// <param name="Enabled">Whether the Subsonic access of the user is switched on.</param>
/// <param name="ProtectedPassword">The encrypted app password; <c>null</c> without one.</param>
public sealed record SubsonicLogin(Guid UserId, string UserName, bool Enabled, string? ProtectedPassword);
