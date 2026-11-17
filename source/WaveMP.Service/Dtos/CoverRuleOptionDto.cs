using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// The cover rule of an album or a track: a priority of its own or the rule of the next level.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<CoverRuleOptionDto>))]
public enum CoverRuleOptionDto
{
    /// <summary>
    /// No rule of its own: a track follows its album, an album the global rule of the library settings.
    /// </summary>
    Inherit,

    /// <summary>
    /// The cover of the album before the cover of the track.
    /// </summary>
    AlbumFirst,

    /// <summary>
    /// The cover of the track before the cover of the album.
    /// </summary>
    TrackFirst,
}
