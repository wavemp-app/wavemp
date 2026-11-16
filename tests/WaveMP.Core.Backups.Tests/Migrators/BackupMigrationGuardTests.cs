using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Backups.Migrators;
using WaveMP.Core.Backups.Options;
using WaveMP.Core.Backups.Services;
using WaveMP.Data.Migrators;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace WaveMP.Core.Backups.Tests.Migrators;

[TestFixture]
public class BackupMigrationGuardTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    private IDatabaseMigrator _migrator = null!;
    private IBackupService _backups = null!;
    private BackupOptions _options = null!;
    private BackupMigrationGuard _guard = null!;

    [SetUp]
    public void SetUp()
    {
        _migrator = Substitute.For<IDatabaseMigrator>();
        _migrator.GetAppliedMigrationsAsync(Arg.Any<CancellationToken>()).Returns(["20261110_InitialCreate"]);
        _migrator.GetPendingMigrationsAsync(Arg.Any<CancellationToken>()).Returns(["20261120_AddSomething"]);
        _backups = Substitute.For<IBackupService>();
        BackupFile created = new("id", "id.dump", _now, 1, BackupTrigger.Migration);
        _backups.CreateAsync(Arg.Any<BackupTrigger>(), Arg.Any<CancellationToken>())
            .Returns(new BackupResult(created, []));
        _options = new BackupOptions();
        _guard = new BackupMigrationGuard(
            _migrator,
            _backups,
            MsOptions.Create(_options),
            NullLogger<BackupMigrationGuard>.Instance);
    }

    [Test]
    public async Task MigrateAsync_WithoutPendingMigrations_NeitherBacksUpNorMigrates()
    {
        _migrator.GetPendingMigrationsAsync(Arg.Any<CancellationToken>()).Returns([]);

        MigrationOutcome outcome = await _guard.MigrateAsync();

        outcome.Should().Be(MigrationOutcome.UpToDate);
        await _backups.DidNotReceive().CreateAsync(Arg.Any<BackupTrigger>(), Arg.Any<CancellationToken>());
        await _migrator.DidNotReceive().MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MigrateAsync_WithPendingMigrationsOfAnExistingDatabase_BacksUpBeforeMigrating()
    {
        MigrationOutcome outcome = await _guard.MigrateAsync();

        outcome.Should().Be(MigrationOutcome.Migrated);
        Received.InOrder(() =>
        {
            _backups.CreateAsync(BackupTrigger.Migration, Arg.Any<CancellationToken>());
            _migrator.MigrateAsync(Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task MigrateAsync_WithANewDatabase_MigratesWithoutBackup()
    {
        _migrator.GetAppliedMigrationsAsync(Arg.Any<CancellationToken>()).Returns([]);

        MigrationOutcome outcome = await _guard.MigrateAsync();

        outcome.Should().Be(MigrationOutcome.Migrated);
        await _backups.DidNotReceive().CreateAsync(Arg.Any<BackupTrigger>(), Arg.Any<CancellationToken>());
        await _migrator.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MigrateAsync_WhenTheBackupFails_AppliesNoMigration()
    {
        _backups.CreateAsync(Arg.Any<BackupTrigger>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BackupFailedException(BackupError.PgDumpUnavailable, "pg_dump is missing"));

        MigrationOutcome outcome = await _guard.MigrateAsync();

        outcome.Should().Be(MigrationOutcome.BackupFailed);
        await _migrator.DidNotReceive().MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MigrateAsync_WhenTheBackupFailsUnexpectedly_AppliesNoMigration()
    {
        _backups.CreateAsync(Arg.Any<BackupTrigger>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("Disk full"));

        MigrationOutcome outcome = await _guard.MigrateAsync();

        outcome.Should().Be(MigrationOutcome.BackupFailed);
        await _migrator.DidNotReceive().MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MigrateAsync_WithBackupBeforeMigrationsSwitchedOff_MigratesWithoutBackup()
    {
        _options.BeforeMigrations = false;

        MigrationOutcome outcome = await _guard.MigrateAsync();

        outcome.Should().Be(MigrationOutcome.Migrated);
        await _backups.DidNotReceive().CreateAsync(Arg.Any<BackupTrigger>(), Arg.Any<CancellationToken>());
        await _migrator.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
    }
}
