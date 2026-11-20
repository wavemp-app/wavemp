namespace WaveMP.Data.Projections;

/// <summary>
/// A play of the history with what the statistics need of its track.
/// </summary>
/// <param name="PlayedAt">When the play started.</param>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Title">Title of the track.</param>
/// <param name="ArtistId">Id of the artist of the track.</param>
/// <param name="ArtistName">Name of the artist of the track.</param>
/// <param name="AlbumId">Id of the album.</param>
/// <param name="AlbumTitle">Title of the album.</param>
/// <param name="AlbumArtistId">Id of the artist of the album.</param>
/// <param name="AlbumArtistName">Name of the artist of the album.</param>
/// <param name="Genre">Genre of the track.</param>
/// <param name="Duration">Duration of the track.</param>
public sealed record PlayStatisticsRow(
    DateTimeOffset PlayedAt,
    Guid TrackId,
    string Title,
    Guid? ArtistId,
    string? ArtistName,
    Guid? AlbumId,
    string? AlbumTitle,
    Guid? AlbumArtistId,
    string? AlbumArtistName,
    string? Genre,
    TimeSpan? Duration);
