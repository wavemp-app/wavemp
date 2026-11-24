using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Data access of the own albums.
/// </summary>
public interface IOwnAlbumRepository
{
    Task<Page<OwnAlbumRow>> ListAsync(OwnAlbumListQuery query, CancellationToken cancellationToken = default);

    Task<OwnAlbumRow?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new own album without songs.
    /// </summary>
    Task AddAsync(OwnAlbum album, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets title, album artist, year and genre. Returns <c>false</c> if the album does not exist.
    /// </summary>
    Task<bool> UpdateAsync(
        Guid id,
        string title,
        string albumArtist,
        int? year,
        string? genre,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the definition with its song rows. Returns <c>false</c> if the album does not exist.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or (with <c>null</c>) removes the cover. Returns <c>false</c> if the album does not exist.
    /// </summary>
    Task<bool> SetCoverAsync(
        Guid id,
        string? fileName,
        string? contentType,
        DateTimeOffset? coverUpdatedAt,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// All song rows of the album: the members by position, then the removed songs by position.
    /// </summary>
    Task<IReadOnlyList<OwnAlbumMemberRow>> ListMembersAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all song rows of the album with <paramref name="members"/> and sets its change time. Returns
    /// <see cref="OwnAlbumSaveStatus.TrackTaken"/> if another own album took one of the songs in the meantime.
    /// </summary>
    Task<OwnAlbumSaveStatus> SaveMembersAsync(
        Guid id,
        IReadOnlyList<OwnAlbumMemberRow> members,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The songs of <paramref name="trackIds"/> that exist, with whether they have audio and the own album they are a
    /// member of.
    /// </summary>
    Task<IReadOnlyList<OwnAlbumCandidateRow>> ListCandidatesAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The cover file names of all own albums.
    /// </summary>
    Task<IReadOnlySet<string>> ListCoverFileNamesAsync(CancellationToken cancellationToken = default);
}
