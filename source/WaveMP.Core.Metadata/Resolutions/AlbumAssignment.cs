namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// The album of a track: its title, its artist and its key.
/// </summary>
/// <param name="Title">Title of the album.</param>
/// <param name="AlbumArtist">Artist of the album: the album artist of its tracks, otherwise their artist; <c>null</c>
/// for albums without a known artist and for compilations formed by the compilation flag without an album artist
/// tag.</param>
/// <param name="Year">Year of the album, the first one its tracks name.</param>
/// <param name="Key">Identity of the album, see <see cref="AlbumKeys"/>.</param>
/// <param name="MusicBrainzReleaseId">The MusicBrainz release of the album, the first one its tracks name; a scan finds
/// the album by it before its key.</param>
/// <param name="AlbumArtistMusicBrainzId">The MusicBrainz artist of <paramref name="AlbumArtist"/>, if its tag names
/// exactly one.</param>
public sealed record AlbumAssignment(
    string Title,
    string? AlbumArtist,
    int? Year,
    string Key,
    Guid? MusicBrainzReleaseId = null,
    Guid? AlbumArtistMusicBrainzId = null);
