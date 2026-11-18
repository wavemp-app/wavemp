namespace WaveMP.Core.Subsonic.Authentication;

/// <summary>
/// The user a request of a Subsonic app signed in as.
/// </summary>
/// <param name="Id">Id of the profile.</param>
/// <param name="Name">Name of the profile.</param>
public sealed record SubsonicUser(Guid Id, string Name);
