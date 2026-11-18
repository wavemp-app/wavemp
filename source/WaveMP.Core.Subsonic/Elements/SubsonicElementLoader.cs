using WaveMP.Core.Subsonic.Responses;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Elements;

/// <summary>
/// Loads what the elements of the Subsonic API show beyond the rows of the library (files, favorites with their time,
/// plays of the user) and creates the elements, in the order of the rows.
/// </summary>
internal sealed class SubsonicElementLoader(
    ISubsonicRepository repository,
    ITrackRepository tracks,
    IAlbumRepository albums)
{
    public async Task<IReadOnlyList<SubsonicElement>> SongsAsync(
        Guid userId,
        IReadOnlyList<TrackRow> rows,
        string name = "song",
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, SubsonicTrackDetails> details = await repository.ListTrackDetailsAsync(
            userId,
            [.. rows.Select(row => row.Id)],
            cancellationToken);
        return [.. rows.Select(row => SubsonicElements.Song(row, details.GetValueOrDefault(row.Id), name))];
    }

    /// <summary>
    /// The songs with the ids in the order of the ids; missing tracks are left out.
    /// </summary>
    public async Task<IReadOnlyList<SubsonicElement>> SongsByIdsAsync(
        Guid userId,
        IReadOnlyList<Guid> ids,
        string name = "song",
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TrackRow> rows = await tracks.ListByIdsAsync(ids, userId, cancellationToken);
        return await SongsAsync(userId, InOrder(rows, ids, row => row.Id), name, cancellationToken);
    }

    public async Task<IReadOnlyList<SubsonicElement>> AlbumsAsync(
        Guid userId,
        IReadOnlyList<AlbumRow> rows,
        string name = "album",
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, SubsonicAlbumDetails> details = await repository.ListAlbumDetailsAsync(
            userId,
            [.. rows.Select(row => row.Id)],
            cancellationToken);
        return [.. rows.Select(row => SubsonicElements.Album(row, details.GetValueOrDefault(row.Id), name))];
    }

    /// <summary>
    /// The albums with the ids in the order of the ids; missing albums are left out.
    /// </summary>
    public async Task<IReadOnlyList<SubsonicElement>> AlbumsByIdsAsync(
        Guid userId,
        IReadOnlyList<Guid> ids,
        string name = "album",
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AlbumRow> rows = await albums.ListByIdsAsync(ids, userId, cancellationToken);
        return await AlbumsAsync(userId, InOrder(rows, ids, row => row.Id), name, cancellationToken);
    }

    public async Task<IReadOnlyList<SubsonicElement>> ArtistsAsync(
        Guid userId,
        IReadOnlyList<ArtistRow> rows,
        string name = "artist",
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, DateTimeOffset> stars = await repository.ListStarredAtAsync(
            userId,
            FavoriteType.Artist,
            [.. rows.Select(row => row.Id)],
            cancellationToken);
        return [.. rows.Select(row => SubsonicElements.Artist(row, StarredAt(stars, row.Id), name))];
    }

    public static DateTimeOffset? StarredAt(IReadOnlyDictionary<Guid, DateTimeOffset> stars, Guid id)
    {
        return stars.TryGetValue(id, out DateTimeOffset starredAt) ? starredAt : null;
    }

    private static List<T> InOrder<T>(IReadOnlyList<T> rows, IReadOnlyList<Guid> ids, Func<T, Guid> idOf)
    {
        var byId = rows.ToDictionary(idOf);
        return [.. ids.Where(byId.ContainsKey).Select(id => byId[id])];
    }
}
