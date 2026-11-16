using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// How much detail the visualizations draw. Auto lets the player choose the level by the frame rate of the device.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<VisualizationQualityDto>))]
public enum VisualizationQualityDto
{
    /// <summary>
    /// The player chooses the level by the frame rate of the device.
    /// </summary>
    Auto,

    /// <summary>
    /// Least detail.
    /// </summary>
    Low,

    /// <summary>
    /// Medium detail.
    /// </summary>
    Medium,

    /// <summary>
    /// Most detail.
    /// </summary>
    High,
}
