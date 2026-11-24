using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Whether an own album was applied to the files: never (Draft), applied and unchanged since (Applied), or changed
/// since it was applied (Changed).
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OwnAlbumStatusDto>))]
public enum OwnAlbumStatusDto
{
    Draft,

    Applied,

    Changed,
}
