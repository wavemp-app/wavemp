using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Sort key of the own albums; ties are sorted by id.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OwnAlbumSortDto>))]
public enum OwnAlbumSortDto
{
    Title,

    UpdatedAt,
}
