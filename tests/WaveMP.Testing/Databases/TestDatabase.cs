using Microsoft.EntityFrameworkCore;
using WaveMP.Data;
using WaveMP.Data.Extensions;

namespace WaveMP.Testing.Databases;

/// <summary>
/// A fresh database with all migrations, for tests of the data access without the service. Dropped on dispose.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(string name)
    {
        Name = name;
        ConnectionString = TestDatabaseSettings.BuildConnectionString(name);
    }

    public string Name { get; }

    public string ConnectionString { get; }

    /// <summary>
    /// Drops a database of the same name and creates it again with all migrations, or with the migrations up to
    /// <paramref name="targetMigration"/> (for tests of a later migration).
    /// </summary>
    public static async Task<TestDatabase> CreateMigratedAsync(string name, string? targetMigration = null)
    {
        TestDatabaseSettings.DropDatabase(name);
        TestDatabase database = new(name);
        await database.MigrateAsync(targetMigration);
        return database;
    }

    /// <summary>
    /// Applies the migrations up to <paramref name="targetMigration"/>, or all of them.
    /// </summary>
    public async Task MigrateAsync(string? targetMigration = null)
    {
        await using WaveMpDbContext dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync(targetMigration);
    }

    /// <summary>
    /// Options for a context on this database, configured like the service's.
    /// </summary>
    public DbContextOptions<WaveMpDbContext> CreateOptions()
    {
        DbContextOptionsBuilder<WaveMpDbContext> builder = new();
        builder.UseWaveMpNpgsql(ConnectionString);
        return builder.Options;
    }

    public WaveMpDbContext CreateDbContext()
    {
        return new WaveMpDbContext(CreateOptions());
    }

    public ValueTask DisposeAsync()
    {
        TestDatabaseSettings.DropDatabase(Name);
        return ValueTask.CompletedTask;
    }
}
