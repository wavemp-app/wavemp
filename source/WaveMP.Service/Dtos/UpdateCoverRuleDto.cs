using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Sets the cover rule of an album or a track.
/// </summary>
public sealed class UpdateCoverRuleDto
{
    /// <summary>
    /// The rule; Inherit removes the rule of its own.
    /// </summary>
    [Required]
    public required CoverRuleOptionDto? Rule { get; init; }
}
