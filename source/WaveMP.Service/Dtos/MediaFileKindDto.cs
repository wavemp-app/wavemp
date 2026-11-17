using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Kind of a media file; Lyrics is a lyrics file (.lrc) with the base name of the track.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<MediaFileKindDto>))]
public enum MediaFileKindDto
{
    Audio,
    Video,
    Image,
    Lyrics,
}
