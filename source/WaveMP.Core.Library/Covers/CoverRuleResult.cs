using WaveMP.Data.Entities;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// The cover rule of an album or a track, or that the album or track does not exist.
/// </summary>
/// <param name="IsFound">Whether the album or track exists.</param>
/// <param name="Rule">The rule of the album or track itself; <c>null</c> if it inherits the rule.</param>
/// <param name="Inherited">The rule it inherits: for an album the global one, for a track the one of its album,
/// otherwise the global one.</param>
public sealed record CoverRuleResult(bool IsFound, CoverPriority? Rule, CoverPriority Inherited)
{
    public static CoverRuleResult NotFound { get; } = new(false, null, CoverPriority.AlbumFirst);

    /// <summary>
    /// The rule that applies: its own, otherwise the inherited one.
    /// </summary>
    public CoverPriority Effective => Rule ?? Inherited;
}
