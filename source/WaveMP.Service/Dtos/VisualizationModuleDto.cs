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
    /// A-Force: light in motion; its presets are its modes, its colors are color schemes.
    /// </summary>
    AForce,

    /// <summary>
    /// BlackCap: organic, floating 3D shapes and light effects; its presets are its modes, its colors are color
    /// schemes.
    /// </summary>
    BlackCap,
}
