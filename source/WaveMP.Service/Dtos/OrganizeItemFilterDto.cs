using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Which items of an organize operation are listed: all, those with a conflict, or those with a warning.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeItemFilterDto>))]
public enum OrganizeItemFilterDto
{
    All,

    Conflicts,

    Warnings,
}
