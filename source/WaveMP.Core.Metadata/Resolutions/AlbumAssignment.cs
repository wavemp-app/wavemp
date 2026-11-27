using WaveMP.Core.Metadata.Artists;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// The album of a track: its title, its artists and its key.
/// </summary>
/// <param name="Title">Title of the album.</param>
/// <param name="AlbumArtists">Artists of the album in their order: the album artists of its tracks, otherwise their
/// main artists; empty for albums without a known artist and for compilations formed by the compilation flag without
/// an album artist tag. Each carries its MusicBrainz artist where the tags name it.</param>
/// <param name="Year">Year of the album, the first one its tracks name.</param>
/// <param name="Key">Identity of the album, see <see cref="AlbumKeys"/>.</param>
/// <param name="MusicBrainzReleaseId">The MusicBrainz release of the album, the first one its tracks name; a scan finds
/// the album by it before its key.</param>
public sealed record AlbumAssignment(
    string Title,
    IReadOnlyList<ArtistName> AlbumArtists,
    int? Year,
    string Key,
    Guid? MusicBrainzReleaseId = null)
{
    /// <summary>
    /// The first artist of the album; <c>null</c> without one.
    /// </summary>
    public string? AlbumArtist => AlbumArtists.Count > 0 ? AlbumArtists[0].Name : null;

    /// <summary>
    /// Equal when all values are equal, the album artists by their content.
    /// </summary>
    public bool Equals(AlbumAssignment? other)
    {
        return other is not null
            && Title == other.Title
            && Year == other.Year
            && Key == other.Key
            && MusicBrainzReleaseId == other.MusicBrainzReleaseId
            && AlbumArtists.SequenceEqual(other.AlbumArtists);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Title, Year, Key, MusicBrainzReleaseId, AlbumArtists.Count);
    }
}
