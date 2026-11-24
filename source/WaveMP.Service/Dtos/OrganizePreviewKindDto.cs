using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What a preview organizes: an own album, or the whole library (or one media source) by the layout template.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizePreviewKindDto>))]
public enum OrganizePreviewKindDto
{
    OwnAlbum,

    Library,
}
