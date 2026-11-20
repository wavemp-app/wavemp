using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Finds the covers that may be shown for a track or an album, also in batches for the whole library, and reads and
/// writes the cover rules of albums and tracks. Nothing is tracked.
/// </summary>
public interface ICoverRepository
{
    /// <summary>
    /// The covers of the track and of its album (never those of other tracks), in no particular order; <c>null</c> if
    /// the track does not exist.
    /// </summary>
    Task<IReadOnlyList<CoverCandidateRow>?> GetTrackCandidatesAsync(
        Guid trackId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The covers of the album and of its tracks, in no particular order; <c>null</c> if the album does not exist.
    /// </summary>
    Task<IReadOnlyList<CoverCandidateRow>?> GetAlbumCandidatesAsync(
        Guid albumId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids of the albums that have covers of their own or tracks with covers, sorted by id.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListAlbumIdsWithCoversAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids of the tracks without album that have covers, sorted by id.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListLooseTrackIdsWithCoversAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The covers of the albums and of their tracks, in no particular order. Covers of a track carry the album of the
    /// track in <see cref="CoverCandidateRow.TrackAlbumId"/>.
    /// </summary>
    Task<IReadOnlyList<CoverCandidateRow>> GetAlbumBatchCandidatesAsync(
        IReadOnlyCollection<Guid> albumIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The covers of the tracks, in no particular order.
    /// </summary>
    Task<IReadOnlyList<CoverCandidateRow>> GetTrackBatchCandidatesAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The cover rule of the album; <c>null</c> if the album does not exist.
    /// </summary>
    Task<CoverRuleRow?> GetAlbumRuleAsync(Guid albumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The cover rule of the track and of its album; <c>null</c> if the track does not exist.
    /// </summary>
    Task<CoverRuleRow?> GetTrackRuleAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the cover rule of the album; <c>null</c> inherits the global rule. <c>false</c> if the album does not
    /// exist.
    /// </summary>
    Task<bool> SetAlbumRuleAsync(Guid albumId, CoverPriority? rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the cover rule of the track; <c>null</c> inherits the rule of its album. <c>false</c> if the track does not
    /// exist.
    /// </summary>
    Task<bool> SetTrackRuleAsync(Guid trackId, CoverPriority? rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// All distinct versions of pictures that covers come from, e.g. to find cached pictures of older versions.
    /// </summary>
    Task<IReadOnlyList<CoverSourceRow>> ListCoverSourcesAsync(CancellationToken cancellationToken = default);
}
