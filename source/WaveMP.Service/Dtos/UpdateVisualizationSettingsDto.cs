using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Users.Settings;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the visualization of the current profile. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateVisualizationSettingsDto
{
    /// <summary>
    /// The visualization. Another module than the current one forgets the preset, unless the request names one.
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
}
