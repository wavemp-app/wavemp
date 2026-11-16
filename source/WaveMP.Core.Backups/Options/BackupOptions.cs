using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Backups.Options;

/// <summary>
/// Technical settings of the database backups (configuration section <c>Backups</c>, e.g.
/// <c>Backups__PgDumpPath</c>). The exports lie in the folder <c>backups</c> of the data folder, which comes from the
/// environment variable <c>DATA_PATH</c> (default <c>/data</c>). Time of day and number of kept exports are server
/// settings, see <see cref="Settings.Groups.BackupSettings"/>.
/// </summary>
public sealed class BackupOptions
{
    public const string SectionName = "Backups";
    public const string FolderName = "backups";

    /// <summary>
    /// Absolute path of the backup folder, set from the data folder (see <see cref="DataPaths"/>).
    /// </summary>
    public string RootPath { get; set; } = DataPaths.DefaultDataPath + "/" + FolderName;

    /// <summary>
    /// pg_dump executable; a name is looked up in the PATH. Its major version must be at least the one of the
    /// database server; the image brings the client of PostgreSQL 18.
    /// </summary>
    public string PgDumpPath { get; set; } = "pg_dump";

    /// <summary>
    /// How long one export may take before pg_dump is stopped and the backup fails.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether the database is exported before pending migrations are applied on start. Only switched off for
    /// development without pg_dump.
    /// </summary>
    public bool BeforeMigrations { get; set; } = true;
}
