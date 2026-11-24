namespace WaveMP.Data.Projections;

/// <summary>
/// An own album with the number of its songs.
/// </summary>
/// <param name="Id">Id of the own album.</param>
/// <param name="Title">Title of the album.</param>
/// <param name="AlbumArtist">Album artist.</param>
/// <param name="Year">Year written into the songs, or <c>null</c>.</param>
/// <param name="Genre">Genre written into the songs, or <c>null</c>.</param>
/// <param name="CoverFileName">File of the cover in the data folder, or <c>null</c>.</param>
/// <param name="CoverContentType">Content type of the cover.</param>
/// <param name="CoverUpdatedAt">When the cover was uploaded.</param>
/// <param name="TrackCount">Number of songs that are members.</param>
/// <param name="RemovedCount">Number of songs taken out since the last apply.</param>
/// <param name="CreatedAt">When the album was created.</param>
/// <param name="UpdatedAt">When the album changed for the last time.</param>
/// <param name="AppliedAt">When the album was applied for the last time.</param>
/// <param name="LibraryAlbumId">The album of the library of its first song, once the album was applied.</param>
public sealed record OwnAlbumRow(
    Guid Id,
    string Title,
    string AlbumArtist,
    int? Year,
    string? Genre,
    string? CoverFileName,
    string? CoverContentType,
    DateTimeOffset? CoverUpdatedAt,
    int TrackCount,
    int RemovedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? AppliedAt,
    Guid? LibraryAlbumId);
