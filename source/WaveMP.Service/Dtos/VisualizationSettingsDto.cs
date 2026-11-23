namespace WaveMP.Service.Dtos;

/// <summary>
/// Visualization of the Now Playing view of the current profile.
/// </summary>
public sealed class VisualizationSettingsDto
{
    /// <summary>
    /// The visualization; Milkdrop by default. The presets of A-Force are its style variants, the
    /// presets of BlackCap its scenes.
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

    /// <summary>
    /// Id of the color scheme of the module the profile chose, separately from the preset, or null for the first
    /// color scheme of the module. The players know the color schemes of their modules; an id a player does not know
    /// is treated like null. Milkdrop has no color schemes.
    /// </summary>
    public required string? ColorScheme { get; init; }

    /// <summary>
    /// How strongly the visualization moves and reacts; Normal by default. Players use Reduced while the device asks
    /// for reduced motion, whatever the profile chose.
    /// </summary>
    public required VisualizationIntensityDto Intensity { get; init; }

    /// <summary>
    /// Speed of the Milkdrop presets in percent of their original speed at 60 frames a second, from 10 to 100; 50 by
    /// default.
    /// </summary>
    public required int MilkdropSpeed { get; init; }
}
