namespace WaveMP.Service.Dtos;

/// <summary>
/// Autoplay of the current profile: when a list ends and repeat is off, the players go on with matching songs (see
/// SelectMyAutoplayTracks).
/// </summary>
public sealed class AutoplaySettingsDto
{
    /// <summary>
    /// Whether the players go on with matching songs when a list ends and repeat is off; true by default.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// Whether autoplay also chooses songs that only have a music video; true by default.
    /// </summary>
    public required bool IncludeVideoOnly { get; init; }
}
