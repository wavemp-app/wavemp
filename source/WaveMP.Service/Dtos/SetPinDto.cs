using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Users.Pins;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Sets or changes the PIN of a profile.
/// </summary>
public sealed class SetPinDto
{
    /// <summary>
    /// The new PIN, 4 to 8 digits.
    /// </summary>
    [Required]
    [RegularExpression(PinRules.Pattern)]
    public required string Pin { get; init; }
}
