using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// State of an item of an organize operation.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeItemStatusDto>))]
public enum OrganizeItemStatusDto
{
    Pending,

    Conflict,

    Running,

    Done,

    Skipped,

    Failed,

    RolledBack,
}
