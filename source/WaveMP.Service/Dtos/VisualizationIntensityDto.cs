using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// How strongly the visualizations move, react and change their brightness. Players also use Reduced while the device
/// asks for reduced motion.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<VisualizationIntensityDto>))]
public enum VisualizationIntensityDto
{
    /// <summary>
    /// The full motion and reaction.
    /// </summary>
    Normal,

    /// <summary>
    /// Calmer motion, weaker reactions, slower Milkdrop presets and slower changes of brightness.
    /// </summary>
    Reduced,
}
