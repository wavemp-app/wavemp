namespace WaveMP.Data.Projections;

/// <summary>
/// What a group of a list shows of its album.
/// </summary>
/// <param name="Id">Id of the album.</param>
/// <param name="Title">Title of the album.</param>
/// <param name="ArtistId">Id of the album artist; <c>null</c> for albums without a known artist.</param>
/// <param name="ArtistName">Name of the album artist.</param>
/// <param name="IsCompilation">Whether the album is a compilation (see the album entity).</param>
/// <param name="HasCover">Whether the album or one of its tracks has a cover, the same rule as
/// <see cref="AlbumRow.HasCover"/>.</param>
public sealed record GroupAlbumRow(
    Guid Id,
    string Title,
    Guid? ArtistId,
    string? ArtistName,
    bool IsCompilation,
    bool HasCover);
