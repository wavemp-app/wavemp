namespace WaveMP.Core.Backups.Files;

/// <summary>
/// The exports in the backup folder (<c>/data/backups</c>). The files themselves are the only record of the exports.
/// </summary>
public interface IBackupStore
{
    /// <summary>
    /// Absolute path of the backup folder.
    /// </summary>
    string RootPath { get; }

    /// <summary>
    /// All exports, newest first; empty if the folder does not exist yet.
    /// </summary>
    IReadOnlyList<BackupFile> List();

    /// <summary>
    /// The export with this id, or <c>null</c> if there is none.
    /// </summary>
    BackupFile? Find(string id);

    /// <summary>
    /// Deletes the export with this id. Returns <c>false</c> if there is none.
    /// </summary>
    bool Delete(string id);

    /// <summary>
    /// Creates the folder if needed, removes files left over by an interrupted export and returns the id of a new
    /// export started at <paramref name="createdAt"/> that no file uses yet.
    /// </summary>
    /// <exception cref="Services.BackupFailedException">The folder cannot be created or written.</exception>
    string Prepare(DateTimeOffset createdAt, BackupTrigger trigger);

    /// <summary>
    /// The path pg_dump writes the export with this id to, before <see cref="Complete"/>.
    /// </summary>
    string PartialPathOf(string id);

    /// <summary>
    /// Turns the written file of the export into the finished export.
    /// </summary>
    /// <exception cref="Services.BackupFailedException">The file cannot be renamed.</exception>
    BackupFile Complete(string id);

    /// <summary>
    /// Removes the written file of an export that failed, if there is one. Never throws.
    /// </summary>
    void Discard(string id);

    /// <summary>
    /// Deletes the oldest exports beyond the newest <paramref name="keep"/> and returns them.
    /// </summary>
    IReadOnlyList<BackupFile> ApplyRetention(int keep);
}
