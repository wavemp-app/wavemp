using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Kind of a file an organize operation moves; AlbumCover is the cover of an own album written from the server.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeFileKindDto>))]
public enum OrganizeFileKindDto
{
    Audio,

    Video,

    Image,

    Lyrics,

    AlbumCover,
}
