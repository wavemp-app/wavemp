using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What the entries of a group of a list have in common: Album (tracks of one album, with album), Artist (tracks
/// without album of one artist, or albums of one album artist, with artist), Letter (the index letter of the text the
/// list is sorted by, with letter), Year (albums of one year, with year) or Unknown (tracks without album and artist,
/// albums without album artist or without year). Clients show their own texts for each kind.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ListGroupKindDto>))]
public enum ListGroupKindDto
{
    Album,
    Artist,
    Letter,
    Year,
    Unknown,
}
