using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// A built-in template of smart playlists; clients name it in the language of the user.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SmartPlaylistTemplateIdDto>))]
public enum SmartPlaylistTemplateIdDto
{
    /// <summary>
    /// Songs rated with 4 stars and more, the best first.
    /// </summary>
    FourStarsAndMore,

    /// <summary>
    /// Songs never played, the newest first.
    /// </summary>
    NeverPlayed,

    /// <summary>
    /// Songs found within the last 30 days, the newest first.
    /// </summary>
    RecentlyAdded,

    /// <summary>
    /// The 50 most played songs.
    /// </summary>
    MostPlayed,

    /// <summary>
    /// The favorite songs, by artist.
    /// </summary>
    Favorites,
}
