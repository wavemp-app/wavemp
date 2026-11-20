namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// The album of a track: its title, its artist and its key.
/// </summary>
/// <param name="Title">Title of the album.</param>
/// <param name="AlbumArtist">Artist of the album: the album artist of its tracks, otherwise their artist; <c>null</c>
/// for albums without a known artist.</param>
/// <param name="Year">Year of the album, the first one its tracks name.</param>
/// <param name="Key">Identity of the album, see <see cref="AlbumKeys"/>.</param>
public sealed record AlbumAssignment(string Title, string? AlbumArtist, int? Year, string Key);
