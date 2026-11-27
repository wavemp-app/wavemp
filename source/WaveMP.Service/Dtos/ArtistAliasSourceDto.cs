using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Where an alias of an artist came from.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ArtistAliasSourceDto>))]
public enum ArtistAliasSourceDto
{
    /// <summary>
    /// Added by an admin.
    /// </summary>
    Admin,

    /// <summary>
    /// Taken from MusicBrainz: an alias or the sort name of the MusicBrainz artist.
    /// </summary>
    MusicBrainz,
}
