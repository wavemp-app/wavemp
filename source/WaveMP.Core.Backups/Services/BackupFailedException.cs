namespace WaveMP.Core.Backups.Services;

/// <summary>
/// An export of the database failed for a known reason. The message is an internal English text for the log, e.g.
/// with the error output of pg_dump.
/// </summary>
public sealed class BackupFailedException(BackupError error, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public BackupError Error { get; } = error;
}
