using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The aliases of the artists (see <see cref="Entities.ArtistAlias"/>). Aliases an admin removed are left out of every
/// read.
/// </summary>
public interface IArtistAliasRepository
{
    /// <summary>
    /// The aliases of an artist sorted by their normalized names; empty for an unknown artist.
    /// </summary>
    Task<Page<ArtistAliasRow>> ListAsync(
        Guid artistId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// An alias of the artist; <c>null</c> if the artist has no such alias.
    /// </summary>
    Task<ArtistAliasRow?> FindAsync(Guid artistId, Guid aliasId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an alias an admin typed. An alias of the artist with the same normalized name that an admin removed comes
    /// back with the new spelling as an alias of the admin.
    /// </summary>
    Task<ArtistAliasAddResult> AddAsync(
        Guid artistId,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an alias: one of the admin is deleted, one of MusicBrainz is marked as removed, so the next lookup does
    /// not add it again. Returns <c>false</c> if the artist has no such alias.
    /// </summary>
    Task<bool> RemoveAsync(Guid artistId, Guid aliasId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the aliases of MusicBrainz of an artist by the given names: aliases of MusicBrainz that are no longer
    /// named are deleted, new names are added unless the artist has the normalized name already (as its name, as an
    /// alias of the admin or as an alias an admin removed). Returns <c>null</c> if the artist does not exist,
    /// otherwise its aliases afterwards.
    /// </summary>
    Task<IReadOnlyList<ArtistAliasRow>?> ReplaceMusicBrainzAliasesAsync(
        Guid artistId,
        IReadOnlyList<string> names,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
