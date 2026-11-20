namespace WaveMP.Service.Dtos;

/// <summary>
/// Videos of the Now Playing view of the current profile.
/// </summary>
public sealed class VideoSettingsDto
{
    /// <summary>
    /// Whether videos show the ambient light: soft light in the colors of the current picture in the free area around
    /// it; true by default. Clients that show videos without a free area around them ignore it.
    /// </summary>
    public required bool AmbientMode { get; init; }
}
