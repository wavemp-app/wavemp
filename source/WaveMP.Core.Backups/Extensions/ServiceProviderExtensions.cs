using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Backups.Migrators;

namespace WaveMP.Core.Backups.Extensions;

public static class ServiceProviderExtensions
{
    /// <summary>
    /// Applies the pending migrations of the database on start, after an export of the database if it already holds
    /// data. Returns <c>false</c> if the export failed: then no migration was applied, the reason is logged and the
    /// service must not start.
    /// </summary>
    public static async Task<bool> MigrateWaveMpDatabaseWithBackupAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        MigrationOutcome outcome = await scope.ServiceProvider
            .GetRequiredService<BackupMigrationGuard>()
            .MigrateAsync(cancellationToken);
        return outcome != MigrationOutcome.BackupFailed;
    }
}
