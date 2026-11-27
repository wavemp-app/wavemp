namespace WaveMP.Data.Projections;

/// <summary>
/// A play of the history with what the statistics need of its track.
/// </summary>
/// <param name="PlayedAt">When the play started.</param>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Title">Title of the track.</param>
/// <param name="ArtistId">Id of the first artist of the track.</param>
/// <param name="ArtistName">Name of the first artist of the track.</param>
/// <param name="AlbumId">Id of the album.</param>
/// <param name="AlbumTitle">Title of the album.</param>
/// <param name="AlbumArtistId">Id of the artist of the album.</param>
/// <param name="AlbumArtistName">Name of the artist of the album.</param>
/// <param name="GenreId">Id of the genre of the track.</param>
/// <param name="Genre">Genre of the track.</param>
/// <param name="Duration">Duration of the track.</param>
/// <param name="Artists">All artists of the track in their order; <c>null</c> where a query does not load them.</param>
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
    Guid? GenreId,
    string? Genre,
    TimeSpan? Duration,
    IReadOnlyList<ArtistCreditRow>? Artists = null)
{
    /// <summary>
    /// All artists of the track: the loaded list, otherwise the first artist alone. A play counts for each of them.
    /// </summary>
    public IReadOnlyList<ArtistCreditRow> AllArtists => Artists is { Count: > 0 } artists
        ? artists
        : ArtistId is { } id ? [new ArtistCreditRow(id, ArtistName ?? string.Empty)] : [];
}
