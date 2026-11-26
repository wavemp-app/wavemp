namespace WaveMP.Service.Dtos;

/// <summary>
/// What an album shows without a cover of its own: its exception, the global rule it inherits and the rule that
/// applies.
/// </summary>
public sealed class AlbumCoverFallbackRuleDto
{
    /// <summary>
    /// The exception of the album; Inherit if it has none.
    /// </summary>
    public required AlbumCoverFallbackOptionDto Fallback { get; init; }

    /// <summary>
    /// The global rule of the library settings.
    /// </summary>
    public required AlbumCoverFallbackDto InheritedFallback { get; init; }

    /// <summary>
    /// The rule that applies: its own, otherwise the inherited one.
    /// </summary>
    public required AlbumCoverFallbackDto EffectiveFallback { get; init; }
}
