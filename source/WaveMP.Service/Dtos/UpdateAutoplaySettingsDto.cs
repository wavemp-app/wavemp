namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the autoplay of the current profile. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateAutoplaySettingsDto
{
    /// <summary>
    /// Whether the players go on with matching songs when a list ends and repeat is off.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Whether autoplay also chooses songs that only have a music video.
    /// </summary>
    public bool? IncludeVideoOnly { get; init; }
}
