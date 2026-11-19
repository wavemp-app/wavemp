namespace WaveMP.Data.Entities;

/// <summary>
/// The field of a track a rule of a smart playlist looks at. Rating, favorite, play count and last played are those of
/// the owner of the smart playlist.
/// </summary>
public enum SmartPlaylistField
{
    /// <summary>
    /// Name of the artist of the track or of the album artist (text).
    /// </summary>
    Artist,

    /// <summary>
    /// Title of the album (text).
    /// </summary>
    Album,

    /// <summary>
    /// Genre of the track (text).
    /// </summary>
    Genre,

    /// <summary>
    /// Year of the track (number).
    /// </summary>
    Year,

    /// <summary>
    /// Stars of the rating, 0 for unrated tracks (number).
    /// </summary>
    Rating,

    /// <summary>
    /// Whether the track is marked as favorite (yes or no).
    /// </summary>
    Favorite,

    /// <summary>
    /// Number of plays in the play history (number).
    /// </summary>
    PlayCount,

    /// <summary>
    /// When the track was played last (days); never played tracks have no value.
    /// </summary>
    LastPlayed,

    /// <summary>
    /// When the track was found for the first time (days).
    /// </summary>
    DateAdded,

    /// <summary>
    /// Duration in whole seconds (number).
    /// </summary>
    Duration,

    /// <summary>
    /// Whether the track has an audio file (yes or no).
    /// </summary>
    HasAudio,

    /// <summary>
    /// Whether the track has a video file (yes or no).
    /// </summary>
    HasVideo,
}
