using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Users.Settings;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the visualization of the current profile. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateVisualizationSettingsDto
{
    /// <summary>
    /// The visualization. Another module than the current one forgets the preset and the color scheme, unless the
    /// request names them.
    /// </summary>
    public VisualizationModuleDto? Module { get; init; }

    /// <summary>
    /// Name of the chosen preset of the module, up to 200 characters; an empty string forgets it, so the player picks
    /// one.
    /// </summary>
    [StringLength(VisualizationSettingsLimits.PresetMaxLength)]
    public string? Preset { get; init; }

    /// <summary>
    /// Whether the visualization changes its preset automatically; the seconds stay when it is turned off.
    /// </summary>
    public bool? AutoChange { get; init; }

    /// <summary>
    /// Seconds after which the visualization changes its preset, from 5 to 600.
    /// </summary>
    [Range(VisualizationSettingsLimits.AutoChangeMinSeconds, VisualizationSettingsLimits.AutoChangeMaxSeconds)]
    public int? AutoChangeSeconds { get; init; }

    /// <summary>
    /// How much detail the visualization draws.
    /// </summary>
    public VisualizationQualityDto? Quality { get; init; }

    /// <summary>
    /// Id of the chosen color scheme of the module, up to 64 characters; an empty string forgets it, so the player
    /// shows the first color scheme of the module.
    /// </summary>
    [StringLength(VisualizationSettingsLimits.ColorSchemeMaxLength)]
    public string? ColorScheme { get; init; }

    /// <summary>
    /// How strongly the visualization moves and reacts.
    /// </summary>
    public VisualizationIntensityDto? Intensity { get; init; }

    /// <summary>
    /// Speed of the Milkdrop presets in percent, from 10 to 100.
    /// </summary>
    [Range(VisualizationSettingsLimits.MilkdropSpeedMin, VisualizationSettingsLimits.MilkdropSpeedMax)]
    public int? MilkdropSpeed { get; init; }
}
