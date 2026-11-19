using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Data access of the online search of metadata and covers: the tracks and albums it looks up and the online covers
/// kept in the data folder. Nothing is tracked.
/// </summary>
public interface IOnlineMetadataRepository
{
    /// <summary>
    /// The track, or <c>null</c> if it does not exist.
    /// </summary>
    Task<OnlineTrackRow?> FindTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The album with its tracks, or <c>null</c> if it does not exist.
    /// </summary>
    Task<OnlineAlbumRow?> FindAlbumAsync(Guid albumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The id of the album of a track, or <c>null</c> if the track has none or does not exist.
    /// </summary>
    Task<Guid?> FindAlbumIdOfTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the online cover of its album or track, replacing an earlier one. Returns the file name of the replaced
    /// cover, so its picture can be removed, or <c>null</c>. <c>false</c> in <c>Saved</c> if the album or track does
    /// not exist (any longer).
    /// </summary>
    Task<(bool Saved, string? ReplacedFileName)> SetOnlineCoverAsync(
        OnlineCover cover,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the online cover of an album or a track. Returns the file name of its picture, or <c>null</c> if there
    /// was none.
    /// </summary>
    Task<string?> RemoveOnlineCoverAsync(
        Guid? albumId,
        Guid? trackId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The file names of all online covers, e.g. to remove pictures whose album or track is gone.
    /// </summary>
    Task<IReadOnlySet<string>> ListOnlineCoverFileNamesAsync(CancellationToken cancellationToken = default);
}
