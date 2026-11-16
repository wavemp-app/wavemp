using WaveMP.Core.Backups.Exports;
using WaveMP.Core.Backups.Services;

namespace WaveMP.Service.Tests.IntegrationTests.Fakes;

/// <summary>
/// Replaces pg_dump in the integration tests: the build machines have no pg_dump of the PostgreSQL version of the test
/// database, and the export itself is tested end to end against the Docker image. Writes a small file, or fails like
/// pg_dump when <see cref="Fails"/> is set.
/// </summary>
public sealed class FakeDatabaseExporter : IDatabaseExporter
{
    /// <summary>
    /// Content of every export.
    /// </summary>
    public static readonly byte[] Content = "PGDMP fake export"u8.ToArray();

    private int _exports;

    /// <summary>
    /// Fails every export with <see cref="BackupError.ExportFailed"/>.
    /// </summary>
    public bool Fails { get; init; }

    /// <summary>
    /// How many exports were requested.
    /// </summary>
    public int Exports => Volatile.Read(ref _exports);

    public async Task ExportAsync(string path, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _exports);
        if (Fails)
        {
            throw new BackupFailedException(BackupError.ExportFailed, "pg_dump: error: the fake export fails");
        }

        await File.WriteAllBytesAsync(path, Content, cancellationToken);
    }
}
