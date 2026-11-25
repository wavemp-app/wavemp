namespace WaveMP.Core.Settings.Groups;

/// <summary>
/// Settings of the database backups under <c>/data/backups</c>. Stored as the settings group <see cref="Key"/>; values
/// missing in the stored JSON keep the defaults below.
/// </summary>
public sealed class BackupSettings
{
    /// <summary>
    /// Key of the settings group.
    /// </summary>
    public const string Key = "backup";

    /// <summary>
    /// Time of day of the daily backup in the time zone of the server (<c>TZ</c>, by default UTC), in hours and
    /// minutes.
    /// </summary>
    public TimeOnly ScheduleTime { get; init; } = new(3, 0);

    /// <summary>
    /// How many exports are kept; after every export the oldest ones beyond this number are deleted.
    /// </summary>
    public int RetentionCount { get; init; } = 7;
}
