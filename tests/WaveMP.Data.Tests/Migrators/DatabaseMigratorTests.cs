using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WaveMP.Data.Extensions;
using WaveMP.Data.Migrators;
using WaveMP.Testing.Databases;
using WaveMP.Testing.Logs;

namespace WaveMP.Data.Tests.Migrators;

/// <summary>
/// The migrations on a new installation: the database container creates an empty database (or none exists yet), and
/// asking for the applied and pending migrations must not log the missing migrations history table as an error.
/// </summary>
[TestFixture]
[Category("Integration")]
public class DatabaseMigratorTests
{
    private const string DatabaseName = "wavemp_tests_migrator";

    private ErrorLogCollector _errorLog = null!;
    private ILoggerFactory _loggerFactory = null!;

    [SetUp]
    public void SetUp()
    {
        _errorLog = new ErrorLogCollector();
        _loggerFactory = new LoggerFactory([_errorLog]);
    }

    [TearDown]
    public void TearDown()
    {
        _loggerFactory.Dispose();
        _errorLog.Dispose();
        TestDatabaseSettings.DropDatabase(DatabaseName);
    }

    [Test]
    public async Task GetAppliedMigrationsAsync_OfAnEmptyDatabase_ReturnsNoneWithoutErrors()
    {
        TestDatabaseSettings.CreateEmptyDatabase(DatabaseName);
        await using WaveMpDbContext dbContext = CreateDbContext();

        IReadOnlyList<string> applied = await new DatabaseMigrator(dbContext).GetAppliedMigrationsAsync();

        applied.Should().BeEmpty();
        _errorLog.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task GetPendingMigrationsAsync_OfAnEmptyDatabase_ReturnsAllMigrationsWithoutErrors()
    {
        TestDatabaseSettings.CreateEmptyDatabase(DatabaseName);
        await using WaveMpDbContext dbContext = CreateDbContext();

        IReadOnlyList<string> pending = await new DatabaseMigrator(dbContext).GetPendingMigrationsAsync();

        pending.Should().Equal(dbContext.Database.GetMigrations());
        _errorLog.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task MigrateAsync_OfAnEmptyDatabase_AppliesAllMigrationsWithoutErrors()
    {
        TestDatabaseSettings.CreateEmptyDatabase(DatabaseName);
        await using WaveMpDbContext dbContext = CreateDbContext();
        DatabaseMigrator migrator = new(dbContext);

        await migrator.GetPendingMigrationsAsync();
        await migrator.GetAppliedMigrationsAsync();
        await migrator.MigrateAsync();

        (await migrator.GetAppliedMigrationsAsync()).Should().Equal(dbContext.Database.GetMigrations());
        (await migrator.GetPendingMigrationsAsync()).Should().BeEmpty();
        _errorLog.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task MigrateAsync_WithoutDatabase_CreatesTheDatabaseAndAppliesAllMigrationsWithoutErrors()
    {
        TestDatabaseSettings.DropDatabase(DatabaseName);
        await using WaveMpDbContext dbContext = CreateDbContext();
        DatabaseMigrator migrator = new(dbContext);

        IReadOnlyList<string> applied = await migrator.GetAppliedMigrationsAsync();
        IReadOnlyList<string> pending = await migrator.GetPendingMigrationsAsync();
        await migrator.MigrateAsync();

        applied.Should().BeEmpty();
        pending.Should().Equal(dbContext.Database.GetMigrations());
        (await migrator.GetPendingMigrationsAsync()).Should().BeEmpty();
        _errorLog.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task GetAppliedMigrationsAsync_OfAPartlyMigratedDatabase_ReturnsTheAppliedMigrations()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            DatabaseName,
            "20261111090059_AddUsers");
        await using WaveMpDbContext dbContext = CreateDbContext();
        DatabaseMigrator migrator = new(dbContext);

        IReadOnlyList<string> applied = await migrator.GetAppliedMigrationsAsync();
        IReadOnlyList<string> pending = await migrator.GetPendingMigrationsAsync();

        applied.Should().Equal("20261110233744_InitialCreate", "20261111090059_AddUsers");
        pending.Should().Equal(dbContext.Database.GetMigrations().Skip(2));
        _errorLog.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// A context configured like the service's, including the retries on transient failures, that logs into
    /// <see cref="_errorLog"/>.
    /// </summary>
    private WaveMpDbContext CreateDbContext()
    {
        DbContextOptionsBuilder<WaveMpDbContext> builder = new();
        builder
            .UseWaveMpNpgsql(
                TestDatabaseSettings.BuildConnectionString(DatabaseName),
                npgsql => npgsql.EnableRetryOnFailure())
            .UseLoggerFactory(_loggerFactory);
        return new WaveMpDbContext(builder.Options);
    }
}
