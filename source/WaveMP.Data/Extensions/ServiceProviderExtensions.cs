using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WaveMP.Data.Migrators;

namespace WaveMP.Data.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Applies all pending EF Core migrations to the WaveMP database, without a backup before. The service backs up
    /// the database before its migrations with <c>MigrateWaveMpDatabaseWithBackupAsync</c> of WaveMP.Core.Backups.
    /// </summary>
    public static async Task MigrateWaveMpDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IDatabaseMigrator migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
        ILogger logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ServiceProviderExtensions));

        IReadOnlyList<string> pending = await migrator.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date");
            return;
        }

        logger.LogInformation(
            "Applying {MigrationCount} database migration(s): {Migrations}",
            pending.Count,
            string.Join(", ", pending));
        await migrator.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations applied");
    }
}
