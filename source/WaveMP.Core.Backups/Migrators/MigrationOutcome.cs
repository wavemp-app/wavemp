namespace WaveMP.Core.Backups.Migrators;

/// <summary>
/// How the migrations on start ended.
/// </summary>
public enum MigrationOutcome
{
    /// <summary>
    /// No migration was pending.
    /// </summary>
    UpToDate,

    /// <summary>
    /// The pending migrations were applied, after an export if the database already held data.
    /// </summary>
    Migrated,

    /// <summary>
    /// The export before the migrations failed, so no migration was applied; the service must not start.
    /// </summary>
    BackupFailed,
}
