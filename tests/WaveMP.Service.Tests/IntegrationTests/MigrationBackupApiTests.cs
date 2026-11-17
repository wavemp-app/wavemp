using AwesomeAssertions;
using Npgsql;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Service.Tests.IntegrationTests.Fakes;
using WaveMP.Testing.Databases;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The export of the database before migrations on start. A pending migration is simulated on an existing database by
/// undoing one migration by hand: its column is dropped and its row in the history removed.
/// </summary>
[TestFixture]
[Category("Integration")]
public class MigrationBackupApiTests
{
    private const string DatabaseName = WaveMpWebApplicationFactory.TestDatabaseName + "_migration_backup";
    private const string UndoneMigration = "20261116113354_AddVisualizationQuality";

    [TearDown]
    public void TearDown()
    {
        TestDatabaseSettings.DropDatabase(DatabaseName);
    }

    [Test]
    public async Task Start_WithPendingMigrationOfAnExistingDatabase_ExportsTheDatabaseBeforeMigrating()
    {
        await CreateDatabaseWithPendingMigrationAsync();

        await using WaveMpWebApplicationFactory factory = new(DatabaseName);
        using HttpClient admin = await factory.CreateAdminClientAsync();
        PageDto<BackupDto> backups = await admin.GetJsonAsync<PageDto<BackupDto>>("/api/v1/backups");

        factory.Exporter.Exports.Should().Be(1);
        backups.Items.Should().ContainSingle().Which.Trigger.Should().Be(BackupTriggerDto.Migration);
        (await IsAppliedAsync()).Should().BeTrue();
    }

    [Test]
    public async Task Start_WhenTheExportBeforeTheMigrationFails_AppliesNoMigrationAndDoesNotStart()
    {
        await CreateDatabaseWithPendingMigrationAsync();

        await using WaveMpWebApplicationFactory factory = new(
            DatabaseName,
            exporter: new FakeDatabaseExporter { Fails = true });
        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>();
        factory.Exporter.Exports.Should().Be(1);
        (await IsAppliedAsync()).Should().BeFalse();
        string folder = Path.Combine(factory.DataPath, "backups");
        (Directory.Exists(folder) ? Directory.EnumerateFiles(folder) : [])
            .Should().BeEmpty("a failed export leaves no file behind");
    }

    [Test]
    public async Task Start_WithNewDatabase_MigratesWithoutExport()
    {
        await using WaveMpWebApplicationFactory factory = new(DatabaseName, freshDatabase: true);
        using HttpClient client = factory.CreateClient();

        factory.Exporter.Exports.Should().Be(0);
        (await IsAppliedAsync()).Should().BeTrue();
        factory.ErrorLog.Errors.Should().BeEmpty("a new installation is no error");
    }

    [Test]
    public async Task Start_WithEmptyDatabaseOfANewInstallation_MigratesWithoutExportAndWithoutErrors()
    {
        // The database container of a new installation creates the database, but no table in it.
        TestDatabaseSettings.CreateEmptyDatabase(DatabaseName);

        await using WaveMpWebApplicationFactory factory = new(DatabaseName);
        using HttpClient client = factory.CreateClient();

        factory.Exporter.Exports.Should().Be(0);
        (await IsAppliedAsync()).Should().BeTrue();
        factory.ErrorLog.Errors.Should().BeEmpty("the missing migrations history table of a new database is expected");
    }

    private static async Task CreateDatabaseWithPendingMigrationAsync()
    {
        await using (WaveMpWebApplicationFactory first = new(DatabaseName, freshDatabase: true))
        {
            using HttpClient client = first.CreateClient();
        }

        await using NpgsqlConnection connection = new(TestDatabaseSettings.BuildConnectionString(DatabaseName));
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE user_settings DROP COLUMN visualization_quality; "
            + "DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @id;";
        command.Parameters.AddWithValue("id", UndoneMigration);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> IsAppliedAsync()
    {
        await using NpgsqlConnection connection = new(TestDatabaseSettings.BuildConnectionString(DatabaseName));
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @id";
        command.Parameters.AddWithValue("id", UndoneMigration);
        return (long)(await command.ExecuteScalarAsync())! == 1;
    }
}
