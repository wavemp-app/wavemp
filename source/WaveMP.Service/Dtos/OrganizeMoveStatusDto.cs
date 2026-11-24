using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// State of one file move of an organize operation.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeMoveStatusDto>))]
public enum OrganizeMoveStatusDto
{
    Pending,

    Moved,

    RolledBack,
}
