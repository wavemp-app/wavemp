using Microsoft.EntityFrameworkCore;

namespace WaveMP.Data.Migrators;

internal sealed class DatabaseMigrator(WaveMpDbContext dbContext) : IDatabaseMigrator
{
    public async Task<IReadOnlyList<string>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
    {
        // Empty for a new database without the history table; WaveMpHistoryRepository checks for it without an error.
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
