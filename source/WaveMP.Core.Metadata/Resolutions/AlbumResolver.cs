using WaveMP.Core.Metadata.Texts;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// Decides the album of every track: an album is the combination of its artist and its title as the metadata names
/// them (see <see cref="AlbumKeys"/>). The artist is the album artist of the track if it has one, so compilations
/// with an album artist such as "Various Artists" and songs with featured artists stay together; otherwise it is the
/// artist of the track. Tracks of one folder with another artist or album title belong to other albums, tracks of
/// other folders with the same artist and title to the same album. No display texts are invented; clients show their
/// own text for albums without artist.
/// </summary>
public static class AlbumResolver
{
    /// <summary>
    /// The album of each track, in the order of <paramref name="tracks"/>; <c>null</c> for tracks without album.
    /// </summary>
    /// <param name="tracks">The tracks of one folder.</param>
    /// <param name="folder">Identifies the album folder of the tracks; keeps albums without any artist apart from
    /// those of other folders.</param>
    public static IReadOnlyList<AlbumAssignment?> Resolve(
        IReadOnlyList<ResolvedTrackMetadata> tracks,
        string? folder = null)
    {
        var assignments = new AlbumAssignment?[tracks.Count];
        IEnumerable<IGrouping<string, int>> albums = Enumerable.Range(0, tracks.Count)
            .Where(index => MetadataText.Normalize(tracks[index].Album) is not null)
            .GroupBy(
                index => AlbumKeys.Create(tracks[index].Album!, ArtistOf(tracks[index]), folder),
                StringComparer.Ordinal);

        foreach (IGrouping<string, int> album in albums)
        {
            ResolvedTrackMetadata[] members = [.. album.Select(index => tracks[index])];
            AlbumAssignment assignment = new(
                MetadataText.Normalize(members[0].Album)!,
                ArtistOf(members[0]),
                members.Select(track => track.Year).FirstOrDefault(value => value is not null),
                album.Key);
            foreach (int index in album)
            {
                assignments[index] = assignment;
            }
        }

        return assignments;
    }

    /// <summary>
    /// The artist that identifies the album of a track: its album artist, otherwise its artist.
    /// </summary>
    public static string? ArtistOf(ResolvedTrackMetadata track)
    {
        return MetadataText.Normalize(track.AlbumArtist) ?? MetadataText.Normalize(track.Artist);
    }
}
