namespace WaveMP.Service.Dtos;

/// <summary>
/// The cover rule of an album or a track and the priority that applies to it.
/// </summary>
public sealed class CoverRuleDto
{
    /// <summary>
    /// The rule of the album or track itself; Inherit if it has none.
    /// </summary>
    public required CoverRuleOptionDto Rule { get; init; }

    /// <summary>
    /// The priority it inherits: for an album the global rule, for a track the rule of its album, otherwise the
    /// global rule.
    /// </summary>
    public required CoverPriorityDto InheritedPriority { get; init; }

    /// <summary>
    /// The priority that applies: its own rule, otherwise the inherited one.
    /// </summary>
    public required CoverPriorityDto EffectivePriority { get; init; }
}
