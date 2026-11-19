using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// The field of a song a rule of a smart playlist looks at, with the kind of its value: text (Artist, Album, Genre),
/// number (Year, Rating, PlayCount, Duration), days (LastPlayed, DateAdded) or yes/no (Favorite, HasAudio, HasVideo).
/// Rating, Favorite, PlayCount and LastPlayed are those of the current profile.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SmartPlaylistFieldDto>))]
public enum SmartPlaylistFieldDto
{
    /// <summary>
    /// Name of the artist of the song or of the album artist (text).
    /// </summary>
    Artist,

    /// <summary>
    /// Title of the album (text).
    /// </summary>
    Album,

    /// <summary>
    /// Genre (text).
    /// </summary>
    Genre,

    /// <summary>
    /// Year, 0 to 9999 (number).
    /// </summary>
    Year,

    /// <summary>
    /// Stars of the rating, 0 (unrated) to 5 (number).
    /// </summary>
    Rating,

    /// <summary>
    /// Whether the song is a favorite (yes/no).
    /// </summary>
    Favorite,

    /// <summary>
    /// Number of plays, 0 to 1000000 (number).
    /// </summary>
    PlayCount,

    /// <summary>
    /// When the song was played last, 1 to 36500 days (days); never played songs are not within any days.
    /// </summary>
    LastPlayed,

    /// <summary>
    /// When the song was found for the first time, 1 to 36500 days (days).
    /// </summary>
    DateAdded,

    /// <summary>
    /// Duration in whole seconds, 0 to 359999 (number).
    /// </summary>
    Duration,

    /// <summary>
    /// Whether the song has an audio file (yes/no).
    /// </summary>
    HasAudio,

    /// <summary>
    /// Whether the song has a music video (yes/no).
    /// </summary>
    HasVideo,
}
