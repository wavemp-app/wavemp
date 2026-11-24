using WaveMP.Core.Library.Albums;
using WaveMP.Core.Library.Artists;
using WaveMP.Core.Library.Queries;
using WaveMP.Core.Library.Tracks;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Groups;

/// <summary>
/// Reads what the groups need of every entry of the list in one query, forms the runs in memory and reads the albums
/// and artists only for the groups of the requested page.
/// </summary>
internal sealed class ListGroupService(
    ITrackRepository tracks,
    IAlbumRepository albums,
    IArtistRepository artists) : IListGroupService
{
    public async Task<Page<ListGroup>> ListTrackGroupsAsync(
        TrackListRequest request,
        CancellationToken cancellationToken = default)
    {
        TrackListQuery query = LibraryQueries.ToQuery(request, TrackSortField.AlbumArtist);
        IReadOnlyList<GroupKeyRow> keys = await tracks.ListGroupKeysAsync(query, cancellationToken);

        // The same groups as the web app forms of the loaded tracks: the album, otherwise the artist of the track.
        IReadOnlyList<GroupRun<GroupKeyRow>> runs = GroupRuns.Of(
            keys,
            row => (row.AlbumId, row.AlbumId is null ? row.ArtistId : null));
        List<GroupRun<GroupKeyRow>> page = PageOf(runs, request.Offset, request.Limit);

        Dictionary<Guid, GroupAlbumRow> albumsById = await AlbumsAsync(
            page.Select(run => run.First.AlbumId),
            cancellationToken);
        Dictionary<Guid, GroupArtistRow> artistsById = await ArtistsAsync(
            page.Where(run => run.First.AlbumId is null).Select(run => run.First.ArtistId),
            cancellationToken);
        bool byYear = query.Sort == TrackSortField.Year;

        List<ListGroup> groups = [.. page.Select(run =>
        {
            GroupKeyRow first = run.First;
            ListGroup group = new(
                ListGroupKind.Unknown,
                run.Position,
                run.Count,
                Letter: LetterOf(first),
                Year: byYear ? first.Year : null);
            if (first.AlbumId is { } albumId)
            {
                return albumsById.TryGetValue(albumId, out GroupAlbumRow? album)
                    ? group with { Kind = ListGroupKind.Album, Album = album }
                    : group;
            }

            return first.ArtistId is { } artistId && artistsById.TryGetValue(artistId, out GroupArtistRow? artist)
                ? group with { Kind = ListGroupKind.Artist, Artist = artist }
                : group;
        })];
        return new Page<ListGroup>(groups, request.Offset, request.Limit, runs.Count);
    }

    public async Task<Page<ListGroup>> ListAlbumGroupsAsync(
        AlbumListRequest request,
        CancellationToken cancellationToken = default)
    {
        AlbumListQuery query = LibraryQueries.ToQuery(request);
        if (query.Sort is not (AlbumSortField.Artist or AlbumSortField.Title or AlbumSortField.Year))
        {
            return Empty(request.Offset, request.Limit);
        }

        IReadOnlyList<GroupKeyRow> keys = await albums.ListGroupKeysAsync(query, cancellationToken);
        if (query.Sort == AlbumSortField.Title)
        {
            return LetterGroups(keys, request.Offset, request.Limit);
        }

        if (query.Sort == AlbumSortField.Year)
        {
            IReadOnlyList<GroupRun<GroupKeyRow>> years = GroupRuns.Of(keys, row => row.Year);
            return new Page<ListGroup>(
                [.. PageOf(years, request.Offset, request.Limit).Select(run => run.First.Year is { } year
                    ? new ListGroup(ListGroupKind.Year, run.Position, run.Count, Year: year)
                    : new ListGroup(ListGroupKind.Unknown, run.Position, run.Count))],
                request.Offset,
                request.Limit,
                years.Count);
        }

        IReadOnlyList<GroupRun<GroupKeyRow>> runs = GroupRuns.Of(keys, row => row.ArtistId);
        List<GroupRun<GroupKeyRow>> page = PageOf(runs, request.Offset, request.Limit);
        Dictionary<Guid, GroupArtistRow> artistsById = await ArtistsAsync(
            page.Select(run => run.First.ArtistId),
            cancellationToken);
        return new Page<ListGroup>(
            [.. page.Select(run =>
                run.First.ArtistId is { } artistId && artistsById.TryGetValue(artistId, out GroupArtistRow? artist)
                    ? new ListGroup(
                        ListGroupKind.Artist,
                        run.Position,
                        run.Count,
                        Artist: artist,
                        Letter: LetterOf(run.First))
                    : new ListGroup(ListGroupKind.Unknown, run.Position, run.Count))],
            request.Offset,
            request.Limit,
            runs.Count);
    }

    public async Task<Page<ListGroup>> ListArtistGroupsAsync(
        ArtistListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArtistListQuery query = LibraryQueries.ToQuery(request);
        if (query.Sort != ArtistSortField.Name)
        {
            return Empty(request.Offset, request.Limit);
        }

        IReadOnlyList<GroupKeyRow> keys = await artists.ListGroupKeysAsync(query, cancellationToken);
        return LetterGroups(keys, request.Offset, request.Limit);
    }

    /// <summary>
    /// Groups by the index letter of the text of the sort key; every entry has one, an empty text is filed under
    /// <see cref="IndexLetters.Other"/>.
    /// </summary>
    private static Page<ListGroup> LetterGroups(IReadOnlyList<GroupKeyRow> keys, int offset, int limit)
    {
        IReadOnlyList<GroupRun<GroupKeyRow>> runs = GroupRuns.Of(keys, row => IndexLetters.Of(row.Initial));
        return new Page<ListGroup>(
            [.. PageOf(runs, offset, limit).Select(run => new ListGroup(
                ListGroupKind.Letter,
                run.Position,
                run.Count,
                Letter: IndexLetters.Of(run.First.Initial)))],
            offset,
            limit,
            runs.Count);
    }

    /// <summary>
    /// The index letter of the text of the sort key; none if the list is not sorted by a text or the entry has none.
    /// </summary>
    private static string? LetterOf(GroupKeyRow row)
    {
        return row.Initial is null ? null : IndexLetters.Of(row.Initial);
    }

    private static List<GroupRun<GroupKeyRow>> PageOf(IReadOnlyList<GroupRun<GroupKeyRow>> runs, int offset, int limit)
    {
        return [.. runs.Skip(offset).Take(limit)];
    }

    private static Page<ListGroup> Empty(int offset, int limit)
    {
        return new Page<ListGroup>([], offset, limit, 0);
    }

    private async Task<Dictionary<Guid, GroupAlbumRow>> AlbumsAsync(
        IEnumerable<Guid?> ids,
        CancellationToken cancellationToken)
    {
        Guid[] distinct = [.. ids.OfType<Guid>().Distinct()];
        IReadOnlyList<GroupAlbumRow> rows = await albums.ListGroupAlbumsAsync(distinct, cancellationToken);
        return rows.ToDictionary(row => row.Id);
    }

    private async Task<Dictionary<Guid, GroupArtistRow>> ArtistsAsync(
        IEnumerable<Guid?> ids,
        CancellationToken cancellationToken)
    {
        Guid[] distinct = [.. ids.OfType<Guid>().Distinct()];
        IReadOnlyList<GroupArtistRow> rows = await artists.ListGroupArtistsAsync(distinct, cancellationToken);
        return rows.ToDictionary(row => row.Id);
    }
}
