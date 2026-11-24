using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// The service an online artist picture comes from.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ArtistPictureProviderDto>))]
public enum ArtistPictureProviderDto
{
    /// <summary>
    /// Wikimedia Commons, found through the Wikidata item of the MusicBrainz artist.
    /// </summary>
    Wikimedia,

    /// <summary>
    /// fanart.tv.
    /// </summary>
    FanartTv,
}
