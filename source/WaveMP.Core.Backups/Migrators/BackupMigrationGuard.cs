using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Backups.Options;
using WaveMP.Core.Backups.Services;
using WaveMP.Data.Migrators;

namespace WaveMP.Core.Backups.Migrators;

/// <summary>
/// Applies the pending migrations of the database on start, but exports the database first, so an update that
/// changes the database can always be undone. A new, empty database is migrated without an export. If the export
/// fails, no migration runs.
/// </summary>
public sealed class BackupMigrationGuard(
    IDatabaseMigrator migrator,
    IBackupService backups,
    IOptions<BackupOptions> options,
    ILogger<BackupMigrationGuard> logger)
{
    public async Task<MigrationOutcome> MigrateAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> pending = await migrator.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date");
            return MigrationOutcome.UpToDate;
        }

        IReadOnlyList<string> applied = await migrator.GetAppliedMigrationsAsync(cancellationToken);
        if (applied.Count == 0)
        {
            logger.LogInformation("The database is new; it needs no backup before its migrations");
        }
        else if (!options.Value.BeforeMigrations)
        {
            logger.LogWarning(
                "The backup before database migrations is switched off (Backups:BeforeMigrations); migrating without one");
        }
        else
        {
            logger.LogInformation(
                "Backing up the database before applying {MigrationCount} migration(s)",
                pending.Count);
            try
            {
                BackupResult result = await backups.CreateAsync(BackupTrigger.Migration, cancellationToken);
                logger.LogInformation(
                    "Backed up the database before its migrations to {BackupFile}",
                    result.Created.FileName);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogCritical(
                    exception,
                    "The backup of the database before its migrations failed, so no migration was applied and WaveMP "
                    + "does not start. Make sure the backup folder {BackupFolder} is writable and pg_dump works, then "
                    + "start WaveMP again. Pending migrations: {Migrations}",
                    options.Value.RootPath,
                    string.Join(", ", pending));
                return MigrationOutcome.BackupFailed;
            }
        }

        logger.LogInformation(
            "Applying {MigrationCount} database migration(s): {Migrations}",
            pending.Count,
            string.Join(", ", pending));
        await migrator.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations applied");
        return MigrationOutcome.Migrated;
    }
}
