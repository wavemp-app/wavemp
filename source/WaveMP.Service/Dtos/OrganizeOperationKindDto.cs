using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What an organize operation does.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeOperationKindDto>))]
public enum OrganizeOperationKindDto
{
    OwnAlbum,

    Library,

    Undo,
}
