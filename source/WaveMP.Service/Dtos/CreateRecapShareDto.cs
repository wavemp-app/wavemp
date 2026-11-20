using System.ComponentModel.DataAnnotations;
using WaveMP.Data.Entities;

namespace WaveMP.Service.Dtos;

/// <summary>
/// A recap to share with the other profiles. Sharing the same period (kind, days and time zone) again changes the
/// design of the existing share.
/// </summary>
public sealed class CreateRecapShareDto
{
    /// <summary>
    /// The pattern of the id of a design: lower-case letters, digits and hyphens.
    /// </summary>
    public const string DesignPattern = "^[a-z0-9-]+$";

    [Required]
    public RecapPeriodDto? Period { get; init; }

    /// <summary>
    /// First day of the period; null or missing from the first play on.
    /// </summary>
    public DateOnly? From { get; init; }

    /// <summary>
    /// Last day of the period.
    /// </summary>
    [Required]
    public DateOnly? To { get; init; }

    /// <summary>
    /// IANA time zone the days are counted in, e.g. Europe/Berlin.
    /// </summary>
    [Required]
    [StringLength(RecapShare.TimeZoneMaxLength, MinimumLength = 1)]
    public string? TimeZone { get; init; }

    /// <summary>
    /// Id of the design of the clients, e.g. aurora.
    /// </summary>
    [Required]
    [StringLength(RecapShare.DesignMaxLength, MinimumLength = 1)]
    [RegularExpression(DesignPattern)]
    public string? Design { get; init; }
}
