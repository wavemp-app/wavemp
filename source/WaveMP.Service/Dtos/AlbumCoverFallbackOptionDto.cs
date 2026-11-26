using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What an album shows without a cover of its own, as an exception from the library settings: a rule of its own (see
/// <see cref="AlbumCoverFallbackDto"/>) or the global rule.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<AlbumCoverFallbackOptionDto>))]
public enum AlbumCoverFallbackOptionDto
{
    /// <summary>
    /// No rule of its own: the global rule of the library settings.
    /// </summary>
    Inherit,

    /// <summary>
    /// The picture of its album artist.
    /// </summary>
    ArtistPicture,

    /// <summary>
    /// No cover.
    /// </summary>
    None,
}
