using WaveMP.Core.Library.Albums;
using WaveMP.Core.Library.Artists;
using WaveMP.Core.Library.Tracks;
using WaveMP.Data.Queries;

namespace WaveMP.Core.Library.Groups;

/// <summary>
/// The groups of the lists of tracks, albums and artists, so a client can show and jump to every group of a long list
/// without loading the whole list. The groups follow the same filters, defaults and sort order as the list of the
/// request; offset and limit of the request page the groups, not the entries.
/// </summary>
public interface IListGroupService
{
    /// <summary>
    /// The groups of the track list: runs of tracks of the same album, tracks without album by their artist, tracks
    /// without both as unknown.
    /// </summary>
    Task<Page<ListGroup>> ListTrackGroupsAsync(
        TrackListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The groups of the album list: by album artist when sorted by artist, by index letter of the title when sorted
    /// by title, by year when sorted by year. The other sort keys have no groups (an empty page).
    /// </summary>
    Task<Page<ListGroup>> ListAlbumGroupsAsync(
        AlbumListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The groups of the artist list: by index letter of the name when sorted by name. The other sort keys have no
    /// groups (an empty page).
    /// </summary>
    Task<Page<ListGroup>> ListArtistGroupsAsync(
        ArtistListRequest request,
        CancellationToken cancellationToken = default);
}
