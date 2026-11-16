using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WaveMP.Core.Backups.Exports;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Backups.Jobs;
using WaveMP.Core.Backups.Migrators;
using WaveMP.Core.Backups.Options;
using WaveMP.Core.Backups.Services;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Extensions;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Backups.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the database backups: the folder <c>backups</c> in the data folder of <c>DATA_PATH</c> (default
    /// <c>/data</c>; relative paths against the content root), settings from the section <c>Backups</c>, the export
    /// with pg_dump, the daily job <c>database-backup</c> and the export before migrations
    /// (<see cref="ServiceProviderExtensions.MigrateWaveMpDatabaseWithBackupAsync"/>). Needs <c>AddWaveMpData</c>,
    /// <c>AddWaveMpSettings</c> and <c>AddWaveMpJobs</c>.
    /// </summary>
    public static IServiceCollection AddWaveMpBackups(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<BackupOptions>()
            .BindConfiguration(BackupOptions.SectionName)
            .Configure<IConfiguration, IServiceProvider>((options, configuration, provider) =>
                options.RootPath = DataPaths.Resolve(
                    configuration,
                    provider.GetService<IHostEnvironment>(),
                    BackupOptions.FolderName));

        services.AddSingleton<IBackupStore, BackupStore>();
        services.AddSingleton<IDatabaseExporter, PgDumpDatabaseExporter>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddScoped<BackupMigrationGuard>();

        services.AddWaveMpJob<DatabaseBackupJob>(JobIds.DatabaseBackup);
        services.AddSingleton<IJobScheduleProvider, DatabaseBackupScheduleProvider>();
        services.AddSingleton<ISettingsChangeHandler<BackupSettings>, DatabaseBackupRescheduler>();

        return services;
    }
}
