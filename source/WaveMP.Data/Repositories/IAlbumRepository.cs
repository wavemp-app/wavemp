using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Reads the albums of the library. Nothing is tracked.
/// </summary>
public interface IAlbumRepository
{
    /// <summary>
    /// Lists the albums sorted and filtered as requested; ties are sorted by id.
    /// </summary>
    Task<Page<AlbumRow>> ListAsync(AlbumListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the groups of the album list need of every album of <see cref="ListAsync"/> with the same filters and
    /// sort order, without paging (offset and limit of the query do not apply): the album, its album artist, its year
    /// and the first character of the text of the sort key (the title for <see cref="AlbumSortField.Title"/>, the
    /// name of the album artist for <see cref="AlbumSortField.Artist"/>, none for the other keys).
    /// </summary>
    Task<IReadOnlyList<GroupKeyRow>> ListGroupKeysAsync(
        AlbumListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What the groups of a list show of the albums with these ids, in no particular order; ids of missing albums are
    /// left out.
    /// </summary>
    Task<IReadOnlyList<GroupAlbumRow>> ListGroupAlbumsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The album with the favorite of <paramref name="userId"/>.
    /// </summary>
    Task<AlbumRow?> FindAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The albums with these ids, in no particular order, with the favorites of <paramref name="userId"/>; ids of
    /// missing albums are left out.
    /// </summary>
    Task<IReadOnlyList<AlbumRow>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
