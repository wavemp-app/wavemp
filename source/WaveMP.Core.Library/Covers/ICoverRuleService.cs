using WaveMP.Data.Entities;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// The exceptions of albums and tracks from the global cover rules of the library settings. The rule of the songs
/// (<see cref="CoverPriority"/>): whether a song shows its own cover or the cover of its album first, or only its own;
/// each level inherits the next one unless it has a rule of its own (track, album, global setting). What an album
/// shows without a cover of its own (<see cref="AlbumCoverFallback"/>): the exception of the album, otherwise the
/// global setting. The rules are stored in the database and stay across scans. A change of the rule of the songs
/// notifies every <see cref="ICoverRuleChangeHandler"/>.
/// </summary>
public interface ICoverRuleService
{
    Task<CoverRuleResult> GetAlbumRuleAsync(Guid albumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the rule of the songs of the album; <c>null</c> inherits the global rule again.
    /// </summary>
    Task<CoverRuleResult> SetAlbumRuleAsync(
        Guid albumId,
        CoverPriority? rule,
        CancellationToken cancellationToken = default);

    Task<CoverRuleResult> GetTrackRuleAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the rule of the track; <c>null</c> inherits the rule of its album again.
    /// </summary>
    Task<CoverRuleResult> SetTrackRuleAsync(
        Guid trackId,
        CoverPriority? rule,
        CancellationToken cancellationToken = default);

    Task<AlbumCoverFallbackResult> GetAlbumFallbackAsync(Guid albumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets what the album shows without a cover of its own; <c>null</c> inherits the global rule again.
    /// </summary>
    Task<AlbumCoverFallbackResult> SetAlbumFallbackAsync(
        Guid albumId,
        AlbumCoverFallback? fallback,
        CancellationToken cancellationToken = default);
}
