namespace WaveMP.Service.Dtos;

/// <summary>
/// Visualization of the Now Playing view of the current profile.
/// </summary>
public sealed class VisualizationSettingsDto
{
    /// <summary>
    /// The visualization; Milkdrop by default. The presets of A-Force are its style variants.
    /// </summary>
    public required VisualizationModuleDto Module { get; init; }

    /// <summary>
    /// Name of the preset of the module the profile chose last, or null to let the player pick one. The players know
    /// the presets of their modules; a name a player does not know is treated like null.
    /// </summary>
    public required string? Preset { get; init; }

    /// <summary>
    /// Whether the visualization changes its preset automatically; true by default. Turning it off keeps the seconds.
    /// </summary>
    public required bool AutoChange { get; init; }

    /// <summary>
    /// Seconds after which the visualization changes its preset while the automatic change is on, from 5 to 600; 30
    /// by default.
    /// </summary>
    public required int AutoChangeSeconds { get; init; }

    /// <summary>
    /// How much detail the visualization draws; Auto by default, which lets the player choose the level by the frame
    /// rate of the device.
    /// </summary>
    public required VisualizationQualityDto Quality { get; init; }
}
