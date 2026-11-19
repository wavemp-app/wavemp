namespace WaveMP.Data.Entities;

/// <summary>
/// Sort key of the tracks of a smart playlist. Tracks without a value (e.g. without year or never played) come last in
/// both directions; ties are sorted by album, disc and track number.
/// </summary>
public enum SmartPlaylistSortField
{
    Title,

    /// <summary>
    /// Artist of the track, then album.
    /// </summary>
    Artist,

    Album,

    Year,

    Genre,

    /// <summary>
    /// Rating of the owner; unrated tracks count as 0.
    /// </summary>
    Rating,

    /// <summary>
    /// Number of plays of the owner.
    /// </summary>
    PlayCount,

    /// <summary>
    /// When the owner played the track last.
    /// </summary>
    LastPlayed,

    /// <summary>
    /// When the track was found for the first time.
    /// </summary>
    DateAdded,

    Duration,
}
