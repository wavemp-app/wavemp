using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// The cover rule of a song or of the songs of an album, as an exception from the library settings: a rule of its own
/// (see <see cref="CoverPriorityDto"/>) or the rule of the next level.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<CoverRuleOptionDto>))]
public enum CoverRuleOptionDto
{
    /// <summary>
    /// No rule of its own: a track follows its album, an album the global rule of the library settings.
    /// </summary>
    Inherit,

    /// <summary>
    /// The cover of the album, otherwise the own cover.
    /// </summary>
    AlbumFirst,

    /// <summary>
    /// The own cover, otherwise the cover of the album.
    /// </summary>
    TrackFirst,

    /// <summary>
    /// Only the own cover.
    /// </summary>
    TrackOnly,
}
