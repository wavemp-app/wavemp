using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What an album shows without a cover of its own (an online cover of the album, a picture in its folder or a picture
/// embedded in its songs). An album never shows the cover of another album or a picture with the name of a song.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<AlbumCoverFallbackDto>))]
public enum AlbumCoverFallbackDto
{
    /// <summary>
    /// The picture of its album artist (in the folder of the artist, uploaded or taken over from the online lookup);
    /// the collage of album covers of an artist without a picture does not count. The default of the library settings.
    /// </summary>
    ArtistPicture,

    /// <summary>
    /// No cover.
    /// </summary>
    None,
}
