namespace WaveMP.Data.Projections;

/// <summary>
/// An album with its album artist and the summary of its tracks.
/// </summary>
/// <param name="Id">Id of the album.</param>
/// <param name="Title">Title of the album.</param>
/// <param name="ArtistId">Id of the album artist; <c>null</c> for albums without a known artist.</param>
/// <param name="ArtistName">Name of the album artist.</param>
/// <param name="IsCompilation">Whether the album is a compilation (see the album entity).</param>
/// <param name="Year">Year of the album.</param>
/// <param name="Genre">The first genre of its tracks in the order of disc and track number.</param>
/// <param name="TrackCount">Number of tracks.</param>
/// <param name="DurationMilliseconds">Sum of the durations of the tracks.</param>
/// <param name="HasCover">Whether the album has a cover of its own (a picture in its folder, a picture embedded in
/// one of its tracks or an online cover) or may show the picture of its album artist instead; the rules of the
/// library settings decide what GetAlbumCover delivers.</param>
/// <param name="FirstSeenAt">When the album was found for the first time.</param>
/// <param name="IsFavorite">Whether the user of the query marked the album as favorite.</param>
public sealed record AlbumRow(
    Guid Id,
    string Title,
    Guid? ArtistId,
    string? ArtistName,
    bool IsCompilation,
    int? Year,
    string? Genre,
    int TrackCount,
    double DurationMilliseconds,
    bool HasCover,
    DateTimeOffset FirstSeenAt,
    bool IsFavorite = false);
