using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Reads the artists of the library. Nothing is tracked. Every artist comes with its pictures
/// (<see cref="ArtistRow.Picture"/>); artists without a picture of their own get the albums of their collage.
/// </summary>
public interface IArtistRepository
{
    /// <summary>
    /// Lists the artists sorted and filtered as requested; ties are sorted by id.
    /// </summary>
    Task<Page<ArtistRow>> ListAsync(ArtistListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the groups of the artist list need of every artist of <see cref="ListAsync"/> with the same filters and
    /// sort order, without paging (offset and limit of the query do not apply): the artist and the first character of
    /// its name for <see cref="ArtistSortField.Name"/> (none for the other keys).
    /// </summary>
    Task<IReadOnlyList<GroupKeyRow>> ListGroupKeysAsync(
        ArtistListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What the groups of a list show of the artists with these ids, in no particular order; ids of missing artists
    /// are left out.
    /// </summary>
    Task<IReadOnlyList<GroupArtistRow>> ListGroupArtistsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The artist with the favorite of <paramref name="userId"/>.
    /// </summary>
    Task<ArtistRow?> FindAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The artists with these ids, in no particular order, with the favorites of <paramref name="userId"/>; ids of
    /// missing artists are left out.
    /// </summary>
    Task<IReadOnlyList<ArtistRow>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
