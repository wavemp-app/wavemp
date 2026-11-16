using WaveMP.Core.Backups.Files;
using WaveMP.Data.Queries;

namespace WaveMP.Core.Backups.Services;

/// <summary>
/// The exports of the database in the backup folder: listing, creating and deleting them.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// One page of the exports, newest first.
    /// </summary>
    Page<BackupFile> List(int offset, int limit);

    /// <summary>
    /// Exports the database into a new file of the backup folder. After scheduled and manual exports, the oldest
    /// exports beyond the number of the backup settings are deleted; exports before migrations keep all others,
    /// because the settings cannot be read before the migrations. Exports never run in parallel.
    /// </summary>
    /// <exception cref="BackupFailedException">The export failed; nothing is left in the folder.</exception>
    Task<BackupResult> CreateAsync(BackupTrigger trigger, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an export. Returns <c>false</c> if there is no export with this id.
    /// </summary>
    bool Delete(string id);
}
