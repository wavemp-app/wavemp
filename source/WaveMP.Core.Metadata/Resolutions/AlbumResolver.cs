using WaveMP.Core.Metadata.Artists;
using WaveMP.Core.Metadata.Texts;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// Decides the album of every track: an album is the combination of its artists and its title as the metadata names
/// them (see <see cref="AlbumKeys"/>). The artists are the album artists of the track if it has any, so compilations
/// with an album artist such as "Various Artists" and songs with featured artists stay together; otherwise they are the
/// main artists of the track (its featured artists do not count, so songs of an album without album artist tag stay
/// together whoever they feature). Tracks of one folder with another artist or album title belong to other albums,
/// tracks of other folders with the same artist and title to the same album. The exception are tracks with the
/// compilation flag and without album artist: their album has no artist and is the combination of its title and album
/// folder, so a compilation that carries only the flag is one album whoever its artists are. No display texts are
/// invented; clients show their own text for albums without artist.
/// </summary>
public static class AlbumResolver
{
    /// <summary>
    /// The album of each track, in the order of <paramref name="tracks"/>; <c>null</c> for tracks without album.
    /// </summary>
    /// <param name="tracks">The tracks of one folder.</param>
    /// <param name="folder">Identifies the album folder of the tracks; keeps albums without artist, also those of
    /// compilations, apart from those of other folders.</param>
    public static IReadOnlyList<AlbumAssignment?> Resolve(
        IReadOnlyList<ResolvedTrackMetadata> tracks,
        string? folder = null)
    {
        var assignments = new AlbumAssignment?[tracks.Count];
        IEnumerable<IGrouping<string, int>> albums = Enumerable.Range(0, tracks.Count)
            .Where(index => MetadataText.Normalize(tracks[index].Album) is not null)
            .GroupBy(
                index => AlbumKeys.CreateForArtists(
                    tracks[index].Album!,
                    [.. ArtistsOf(tracks[index]).Select(artist => artist.Name)],
                    folder),
                StringComparer.Ordinal);

        foreach (IGrouping<string, int> album in albums)
        {
            ResolvedTrackMetadata[] members = [.. album.Select(index => tracks[index])];
            AlbumAssignment assignment = new(
                MetadataText.Normalize(members[0].Album)!,
                ArtistsOf(members[0]),
                members.Select(track => track.Year).FirstOrDefault(value => value is not null),
                album.Key,
                members.Select(track => track.MusicBrainzReleaseId).FirstOrDefault(value => value is not null));
            foreach (int index in album)
            {
                assignments[index] = assignment;
            }
        }

        return assignments;
    }

    /// <summary>
    /// The artists that identify the album of a track, as main artists with their MusicBrainz artists: its album
    /// artists, otherwise its main artists (all its artists if none is a main artist); empty for a track with the
    /// compilation flag and without album artist, whose album is identified by its folder instead.
    /// </summary>
    public static IReadOnlyList<ArtistName> ArtistsOf(ResolvedTrackMetadata track)
    {
        if (track.AlbumArtists.Count > 0)
        {
            return track.AlbumArtists;
        }

        if (track.IsCompilation)
        {
            return [];
        }

        ArtistName[] main = [.. track.Artists.Where(artist => artist.Role == ArtistRole.Main)];
        return main.Length > 0 ? main : [.. track.Artists.Select(artist => artist with { Role = ArtistRole.Main })];
    }
}
