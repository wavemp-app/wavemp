using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Users.Pins;
using WaveMP.Service.Validators;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Selects a profile.
/// </summary>
public sealed class CreateSessionDto
{
    /// <summary>
    /// Id of the profile to select.
    /// </summary>
    [RequiredValue]
    public required Guid ProfileId { get; init; }

    /// <summary>
    /// PIN of the profile, 4 to 8 digits; only needed for profiles with <c>hasPin</c>.
    /// </summary>
    [RegularExpression(PinRules.Pattern)]
    public string? Pin { get; init; }
}
