using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What a list in a player was started from.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<PlaybackSourceKindDto>))]
public enum PlaybackSourceKindDto
{
    /// <summary>
    /// The songs of an album.
    /// </summary>
    Album,

    /// <summary>
    /// The songs of an artist.
    /// </summary>
    Artist,

    /// <summary>
    /// The songs of a playlist.
    /// </summary>
    Playlist,

    /// <summary>
    /// The songs of a smart playlist.
    /// </summary>
    SmartPlaylist,
}
