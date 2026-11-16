using Microsoft.EntityFrameworkCore;

namespace WaveMP.Data.Migrators;

internal sealed class DatabaseMigrator(WaveMpDbContext dbContext) : IDatabaseMigrator
{
    public async Task<IReadOnlyList<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
    {
        // On an empty database EF Core logs a failed query for the not yet existing history table. This is expected.
        return [.. await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)];
    }

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        return [.. await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)];
    }

    public Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Database.MigrateAsync(cancellationToken);
    }
}
