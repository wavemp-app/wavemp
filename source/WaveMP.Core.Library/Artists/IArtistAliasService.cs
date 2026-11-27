using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Artists;

/// <summary>
/// The aliases of the artists that the search finds them by (see <see cref="Data.Entities.ArtistAlias"/>); admins add
/// and remove them. The aliases of MusicBrainz come from the online lookup.
/// </summary>
public interface IArtistAliasService
{
    /// <summary>
    /// Longest alias; longer ones are cut like names from tags.
    /// </summary>
    public const int MaxNameLength = Data.Texts.NameKeys.MaxLength;

    /// <summary>
    /// The aliases of an artist sorted by their normalized names; <c>null</c> if the artist does not exist.
    /// </summary>
    Task<Page<ArtistAliasRow>?> ListAsync(
        Guid artistId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// An alias of an artist; <c>null</c> if the artist has no such alias.
    /// </summary>
    Task<ArtistAliasRow?> GetAsync(Guid artistId, Guid aliasId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an alias, cleaned like names from tags (white space joined, control characters removed, cut to
    /// <see cref="MaxNameLength"/>).
    /// </summary>
    Task<ArtistAliasAddResult> AddAsync(Guid artistId, string? name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an alias; <c>false</c> if the artist has no such alias.
    /// </summary>
    Task<bool> RemoveAsync(Guid artistId, Guid aliasId, CancellationToken cancellationToken = default);
}
