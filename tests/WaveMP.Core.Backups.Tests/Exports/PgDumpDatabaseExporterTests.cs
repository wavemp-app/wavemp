using AwesomeAssertions;
using WaveMP.Core.Backups.Exports;
using WaveMP.Core.Backups.Options;
using WaveMP.Core.Backups.Services;
using WaveMP.Data.Options;
using WaveMP.Testing.Files;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace WaveMP.Core.Backups.Tests.Exports;

[TestFixture]
public class PgDumpDatabaseExporterTests
{
    [Test]
    public void Build_Always_ExportsInTheCompressedCustomFormatWithoutPasswordOnTheCommandLine()
    {
        DatabaseOptions database = new()
        {
            Host = "wavemp-db",
            Port = 5433,
            Name = "music",
            User = "player",
            Password = "secret",
        };

        IReadOnlyList<string> arguments = PgDumpArguments.Build(database, "/data/backups/export.dump.partial");

        arguments.Should().Equal(
            "--format=custom",
            "--no-password",
            "--host=wavemp-db",
            "--port=5433",
            "--username=player",
            "--dbname=music",
            "--file=/data/backups/export.dump.partial");
        arguments.Should().NotContain(argument => argument.Contains("secret"));
    }

    [Test]
    public async Task ExportAsync_WithoutPgDump_ThrowsPgDumpUnavailable()
    {
        using TemporaryDirectory folder = new();
        PgDumpDatabaseExporter exporter = new(
            MsOptions.Create(new BackupOptions { PgDumpPath = folder.Combine("missing/pg_dump") }),
            MsOptions.Create(new DatabaseOptions { Password = "secret" }));

        Func<Task> export = () => exporter.ExportAsync(folder.Combine("export.dump"));

        (await export.Should().ThrowAsync<BackupFailedException>())
            .Which.Error.Should().Be(BackupError.PgDumpUnavailable);
    }

    [Test]
    public void Shorten_WithLongErrorOutput_KeepsTheStart()
    {
        string output = "  pg_dump: error: " + new string('x', 5000) + "\n";

        string shortened = PgDumpDatabaseExporter.Shorten(output);

        shortened.Should().HaveLength(PgDumpDatabaseExporter.MaxErrorOutputLength);
        shortened.Should().StartWith("pg_dump: error: ");
    }
}
