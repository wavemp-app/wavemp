namespace WaveMP.Core.Backups.Services;

/// <summary>
/// Why an export failed.
/// </summary>
public enum BackupError
{
    /// <summary>
    /// The backup folder cannot be created or written.
    /// </summary>
    FolderUnwritable,

    /// <summary>
    /// pg_dump cannot be started.
    /// </summary>
    PgDumpUnavailable,

    /// <summary>
    /// pg_dump ended with an error or took too long.
    /// </summary>
    ExportFailed,
}
