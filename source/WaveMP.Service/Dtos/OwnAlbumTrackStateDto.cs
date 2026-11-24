using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Whether a song is a member of an own album, or was taken out after the album was applied (the next apply restores
/// its original tags).
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OwnAlbumTrackStateDto>))]
public enum OwnAlbumTrackStateDto
{
    Member,

    Removed,
}
