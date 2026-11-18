namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the Subsonic access of the current profile. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateSubsonicAccessDto
{
    /// <summary>
    /// True allows Subsonic apps to sign in with the app password, false forbids it.
    /// </summary>
    public bool? Enabled { get; init; }
}
