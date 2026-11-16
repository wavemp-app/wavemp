namespace WaveMP.Service.Dtos;

/// <summary>
/// An export of the database in the backup folder (<c>/data/backups</c>).
/// </summary>
public sealed class BackupDto
{
    /// <summary>
    /// Stable id: the file name without its extension, e.g. <c>wavemp-20261116T030000Z-schedule</c>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Name of the file in the backup folder, a compressed dump for pg_restore.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// When the export was started.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    public required long SizeBytes { get; init; }

    public required BackupTriggerDto Trigger { get; init; }
}
