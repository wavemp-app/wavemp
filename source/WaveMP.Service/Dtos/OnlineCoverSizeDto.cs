using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Size of the preview of a cover from the Cover Art Archive.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OnlineCoverSizeDto>))]
public enum OnlineCoverSizeDto
{
    /// <summary>
    /// 250 pixels on the longer side.
    /// </summary>
    Small,

    /// <summary>
    /// 500 pixels on the longer side.
    /// </summary>
    Large,
}
