using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What the remux of a video does with its sound; the picture is always copied.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<RemuxAudioDto>))]
public enum RemuxAudioDto
{
    /// <summary>
    /// The video has no sound.
    /// </summary>
    None,

    /// <summary>
    /// The sound is copied as it is.
    /// </summary>
    Copy,

    /// <summary>
    /// The sound is converted to AAC.
    /// </summary>
    Aac,
}
