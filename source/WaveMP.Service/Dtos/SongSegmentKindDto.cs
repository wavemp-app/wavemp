using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// The file of a track whose song segment is meant: the primary audio file or the primary video.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SongSegmentKindDto>))]
public enum SongSegmentKindDto
{
    Audio,
    Video,
}
