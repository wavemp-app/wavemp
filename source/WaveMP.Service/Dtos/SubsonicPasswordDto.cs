namespace WaveMP.Service.Dtos;

/// <summary>
/// A new app password for Subsonic apps. It is shown this once; the server keeps it encrypted and never returns it
/// again.
/// </summary>
public sealed class SubsonicPasswordDto
{
    /// <summary>
    /// The app password; also the OpenSubsonic API key.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// The Subsonic access with the new password.
    /// </summary>
    public required SubsonicAccessDto Access { get; init; }
}
