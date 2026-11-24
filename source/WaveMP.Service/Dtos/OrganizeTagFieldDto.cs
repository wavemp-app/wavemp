using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// A tag an organize operation writes into an audio file.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeTagFieldDto>))]
public enum OrganizeTagFieldDto
{
    Album,

    AlbumArtist,

    TrackNumber,

    TrackCount,

    DiscNumber,

    Year,

    Genre,
}
