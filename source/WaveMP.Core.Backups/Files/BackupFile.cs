namespace WaveMP.Core.Backups.Files;

/// <summary>
/// An export of the database in the backup folder.
/// </summary>
/// <param name="Id">Stable id: the file name without its extension, e.g.
/// <c>wavemp-20261116T030000Z-schedule</c>.</param>
/// <param name="FileName">Name of the file in the backup folder.</param>
/// <param name="CreatedAt">When the export was started (UTC, to the second).</param>
/// <param name="SizeBytes">Size of the file.</param>
/// <param name="Trigger">What created the export.</param>
public sealed record BackupFile(
    string Id,
    string FileName,
    DateTimeOffset CreatedAt,
    long SizeBytes,
    BackupTrigger Trigger);
