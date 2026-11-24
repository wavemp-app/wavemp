using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Why an organize operation failed or stopped. Clients translate it.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeErrorCodeDto>))]
public enum OrganizeErrorCodeDto
{
    [JsonStringEnumMemberName("unexpected")]
    Unexpected,

    [JsonStringEnumMemberName("item_failed")]
    ItemFailed,

    [JsonStringEnumMemberName("interrupted")]
    Interrupted,

    [JsonStringEnumMemberName("media_read_only")]
    MediaReadOnly,
}
