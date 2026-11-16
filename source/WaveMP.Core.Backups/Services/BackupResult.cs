using WaveMP.Core.Backups.Files;

namespace WaveMP.Core.Backups.Services;

/// <summary>
/// A finished export and the old exports the retention deleted after it.
/// </summary>
public sealed record BackupResult(BackupFile Created, IReadOnlyList<BackupFile> Deleted);
