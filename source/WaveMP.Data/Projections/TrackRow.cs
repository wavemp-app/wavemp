namespace WaveMP.Data.Projections;

/// <summary>
/// A track with its artists and album.
/// </summary>
/// <param name="Id">Id of the track.</param>
/// <param name="Title">Title of the track.</param>
/// <param name="ArtistId">Id of the first artist of the track.</param>
/// <param name="ArtistName">Name of the first artist of the track.</param>
/// <param name="AlbumId">Id of the album.</param>
/// <param name="AlbumTitle">Title of the album.</param>
/// <param name="AlbumArtistId">Id of the first album artist.</param>
/// <param name="AlbumArtistName">Name of the first album artist.</param>
/// <param name="AlbumIsCompilation">Whether the album is a compilation.</param>
/// <param name="TrackNumber">Number of the track on its disc.</param>
/// <param name="DiscNumber">Number of the disc.</param>
/// <param name="Year">Year of the track.</param>
/// <param name="Genre">Genre of the track.</param>
/// <param name="Duration">Duration of the primary audio file, otherwise of the primary video file.</param>
/// <param name="HasAudio">Whether an audio file exists.</param>
/// <param name="HasVideo">Whether a video file exists.</param>
/// <param name="HasCover">Whether a cover may be shown: the track's own or one of its album (also a picture
/// embedded in another track of the album and online covers); the cover rules decide what GetTrackCover delivers.
/// </param>
/// <param name="FirstSeenAt">When the track was found for the first time.</param>
/// <param name="IsFavorite">Whether the user of the query marked the track as favorite.</param>
/// <param name="Rating">Rating of the user of the query, 1 to 5 stars; 0 if the user did not rate the track.</param>
/// <param name="Artists">All artists of the track in their order, with their roles; <c>null</c> where a query does not
/// load them (then the first artist stands for all).</param>
/// <param name="AlbumArtists">All album artists of its album in their order; <c>null</c> where a query does not load
/// them.</param>
public sealed record TrackRow(
    Guid Id,
    string Title,
    Guid? ArtistId,
    string? ArtistName,
    Guid? AlbumId,
    string? AlbumTitle,
    Guid? AlbumArtistId,
    string? AlbumArtistName,
    bool AlbumIsCompilation,
    int? TrackNumber,
    int? DiscNumber,
    int? Year,
    string? Genre,
    TimeSpan? Duration,
    bool HasAudio,
    bool HasVideo,
    bool HasCover,
    DateTimeOffset FirstSeenAt,
    bool IsFavorite = false,
    int Rating = 0,
    IReadOnlyList<ArtistCreditRow>? Artists = null,
    IReadOnlyList<ArtistCreditRow>? AlbumArtists = null)
{
    /// <summary>
    /// All artists of the track in their order: the loaded list, otherwise the first artist alone.
    /// </summary>
    public IReadOnlyList<ArtistCreditRow> AllArtists => Artists is { Count: > 0 } artists
        ? artists
        : ArtistId is { } id && ArtistName is { } name ? [new ArtistCreditRow(id, name)] : [];

    /// <summary>
    /// All album artists of its album in their order: the loaded list, otherwise the first album artist alone.
    /// </summary>
    public IReadOnlyList<ArtistCreditRow> AllAlbumArtists => AlbumArtists is { Count: > 0 } artists
        ? artists
        : AlbumArtistId is { } id && AlbumArtistName is { } name ? [new ArtistCreditRow(id, name)] : [];
}
