using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Role of an artist on a song: a main artist or a featured artist (e.g. K.Flay in "Bob Sinclar feat. K.Flay").
/// Album artists are always main artists.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ArtistRoleDto>))]
public enum ArtistRoleDto
{
    Main,
    Featured,
}
