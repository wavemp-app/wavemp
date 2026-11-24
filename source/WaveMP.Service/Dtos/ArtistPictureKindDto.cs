using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What an artist shows as its picture, in the order of priority.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ArtistPictureKindDto>))]
public enum ArtistPictureKindDto
{
    /// <summary>
    /// The picture file in the folder of the artist (e.g. artist.jpg), delivered by GetArtistPicture.
    /// </summary>
    Local,

    /// <summary>
    /// The picture an admin uploaded, delivered by GetArtistPicture.
    /// </summary>
    Uploaded,

    /// <summary>
    /// The picture an admin took over from the online lookup, delivered by GetArtistPicture; its credit is in
    /// onlineCredit.
    /// </summary>
    Online,

    /// <summary>
    /// No picture of its own: clients show a collage of the covers of the albums in albumIds.
    /// </summary>
    Albums,

    /// <summary>
    /// Neither a picture nor an album with a cover.
    /// </summary>
    None,
}
