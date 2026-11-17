using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Which cover comes first: the cover of the album or the cover of the track. If the preferred cover is missing, the
/// other one is shown.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<CoverPriorityDto>))]
public enum CoverPriorityDto
{
    /// <summary>
    /// The cover of the album (a picture in its folder) before the cover of the track.
    /// </summary>
    AlbumFirst,

    /// <summary>
    /// The cover of the track (a picture with its base name or embedded in its files) before the cover of the album.
    /// </summary>
    TrackFirst,
}
