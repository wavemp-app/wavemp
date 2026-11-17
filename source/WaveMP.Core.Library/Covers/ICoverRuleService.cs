using WaveMP.Data.Entities;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// The cover rules of albums and tracks: whether the cover of the album or the cover of the track comes first. Each
/// level inherits the next one unless it has a rule of its own (track, album, global setting); if the preferred cover
/// is missing, the other one is shown. The rules are stored in the database and stay across scans. A change notifies
/// every <see cref="ICoverRuleChangeHandler"/>.
/// </summary>
public interface ICoverRuleService
{
    Task<CoverRuleResult> GetAlbumRuleAsync(Guid albumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the rule of the album; <c>null</c> inherits the global rule again.
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
}
