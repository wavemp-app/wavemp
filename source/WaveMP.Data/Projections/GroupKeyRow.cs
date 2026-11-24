namespace WaveMP.Data.Projections;

/// <summary>
/// What the groups of a list need of one of its entries, in the order of the list: the album and artist that group
/// the entry, its year and the first character of the text it is sorted by.
/// </summary>
/// <param name="AlbumId">Album of the entry: the album of a track, the album itself in the album list; <c>null</c>
/// for tracks without album and in the artist list.</param>
/// <param name="ArtistId">Artist of the entry: the artist of a track, the album artist of an album, the artist itself
/// in the artist list; <c>null</c> if unknown.</param>
/// <param name="Year">Year of the track or album; <c>null</c> if unknown and in the artist list.</param>
/// <param name="Initial">First character of the text the list is sorted by (for example the album title when sorted
/// by title), empty for an empty text; <c>null</c> if the entry has no such text or the list is not sorted by a
/// text.</param>
public sealed record GroupKeyRow(Guid? AlbumId, Guid? ArtistId, int? Year, string? Initial);
