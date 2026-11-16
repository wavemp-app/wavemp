using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the settings of the database backups. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateBackupSettingsDto
{
    /// <summary>
    /// Time of day of the daily backup as <c>HH:mm</c> (24 hours, e.g. <c>03:00</c>); the backup is planned again at
    /// once.
    /// </summary>
    [RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    public string? ScheduleTime { get; init; }

    /// <summary>
    /// How many exports are kept; a lower number applies with the next export.
    /// </summary>
    [Range(1, 100)]
    public int? RetentionCount { get; init; }
}
