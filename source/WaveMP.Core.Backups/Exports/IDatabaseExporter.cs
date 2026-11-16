namespace WaveMP.Core.Backups.Exports;

/// <summary>
/// Exports the WaveMP database into one compressed file.
/// </summary>
public interface IDatabaseExporter
{
    /// <summary>
    /// Writes the export to <paramref name="path"/>; an existing file is replaced.
    /// </summary>
    /// <exception cref="Services.BackupFailedException">pg_dump cannot be started, fails or takes too long.</exception>
    Task ExportAsync(string path, CancellationToken cancellationToken = default);
}
