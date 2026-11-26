using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Which cover a song shows. The own cover of a song is a cover selected for it in its files, an online cover of the
/// song and a picture with its name (Song.jpg); the cover of its album is the online cover of the album, a picture in
/// the folder of the album and a picture embedded in the songs of the album. A song never shows the picture of an
/// artist.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<CoverPriorityDto>))]
public enum CoverPriorityDto
{
    /// <summary>
    /// The cover of the album, otherwise the own cover.
    /// </summary>
    AlbumFirst,

    /// <summary>
    /// The own cover, otherwise the cover of the album (the default of the library settings).
    /// </summary>
    TrackFirst,

    /// <summary>
    /// Only the own cover; without one the song shows no cover.
    /// </summary>
    TrackOnly,
}
