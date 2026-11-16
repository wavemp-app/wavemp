namespace WaveMP.Data.Migrators;

/// <summary>
/// The EF Core migrations of the WaveMP database: which are applied, which are pending, and applying them.
/// </summary>
public interface IDatabaseMigrator
{
    /// <summary>
    /// Ids of the migrations already applied to the database; empty for a new, empty database.
    /// </summary>
    Task<IReadOnlyList<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids of the migrations of this version that are not applied yet.
    /// </summary>
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies all pending migrations.
    /// </summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
