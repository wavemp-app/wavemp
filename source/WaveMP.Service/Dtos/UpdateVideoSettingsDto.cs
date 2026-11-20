namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the videos of the Now Playing view of the current profile. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateVideoSettingsDto
{
    /// <summary>
    /// Whether videos show the ambient light in the free area around their picture.
    /// </summary>
    public bool? AmbientMode { get; init; }
}
