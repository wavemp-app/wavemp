namespace WaveMP.Service.Dtos;

/// <summary>
/// Settings of the database backups.
/// </summary>
public sealed class BackupSettingsDto
{
    /// <summary>
    /// Time of day of the daily backup as <c>HH:mm</c> (24 hours), in the time zone of the server (<c>TZ</c>, by
    /// default UTC).
    /// </summary>
    public required string ScheduleTime { get; init; }

    /// <summary>
    /// How many exports are kept (1 to 100); after every daily or manual export the oldest ones beyond this number
    /// are deleted.
    /// </summary>
    public required int RetentionCount { get; init; }
}
