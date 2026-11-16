using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// A visualization of the players: an engine that draws the music, with its own presets. Further modules may be added
/// in later versions; clients fall back to their default for a module they do not know.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<VisualizationModuleDto>))]
public enum VisualizationModuleDto
{
    /// <summary>
    /// Milkdrop presets.
    /// </summary>
    Milkdrop,

    /// <summary>
    /// A-Force: flowing waveforms and particles; its presets are style variants.
    /// </summary>
    AForce,

    /// <summary>
    /// BlackCap: organic, floating 3D shapes and light effects; its presets are scenes.
    /// </summary>
    BlackCap,
}
