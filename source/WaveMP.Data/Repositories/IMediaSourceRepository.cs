using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The media sources of the library.
/// </summary>
public interface IMediaSourceRepository
{
    /// <summary>
    /// All sources, sorted by name, not tracked.
    /// </summary>
    Task<IReadOnlyList<MediaSource>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// A source, tracked for <see cref="UpdateAsync"/>.
    /// </summary>
    Task<MediaSource?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        string normalizedName,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default);

    /// <exception cref="MediaSourceNameConflictException">Another source has the same name.</exception>
    Task AddAsync(MediaSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the changes of a tracked source.
    /// </summary>
    /// <exception cref="MediaSourceNameConflictException">Another source has the same name.</exception>
    Task UpdateAsync(MediaSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a source with its tracks and files, and afterwards the albums and artists left without tracks. Returns
    /// <c>false</c> if the source does not exist.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The number of tracks and files per source; sources without any are missing.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, MediaSourceCounts>> CountAsync(CancellationToken cancellationToken = default);
}
