using WaveMP.Data.Entities;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// A cover rule that changed: other covers may be shown now.
/// </summary>
/// <param name="Level">The level of the rule.</param>
/// <param name="OwnerId">Id of the album or track; <c>null</c> for the global rule.</param>
/// <param name="Previous">The rule before; <c>null</c> if the level inherited the rule.</param>
/// <param name="Current">The rule now; <c>null</c> if the level inherits the rule.</param>
public sealed record CoverRuleChange(
    CoverRuleLevel Level,
    Guid? OwnerId,
    CoverPriority? Previous,
    CoverPriority? Current);
