namespace WaveMP.Core.Backups.Files;

/// <summary>
/// What created an export. Part of its file name.
/// </summary>
public enum BackupTrigger
{
    /// <summary>
    /// The daily schedule.
    /// </summary>
    Schedule,

    /// <summary>
    /// An admin, through the API.
    /// </summary>
    Manual,

    /// <summary>
    /// The start of a new version, before it applied pending migrations of the database.
    /// </summary>
    Migration,
}
