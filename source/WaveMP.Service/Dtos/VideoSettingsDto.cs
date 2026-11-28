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

    /// <summary>
    /// Whether the cover and the visualization skip intro and outro of the sound of a music video for tracks without
    /// an audio file, if the song analysis is sure where the song lies; true by default. The video view always plays
    /// the whole video.
    /// </summary>
    public required bool SkipIntroAndOutro { get; init; }
}
