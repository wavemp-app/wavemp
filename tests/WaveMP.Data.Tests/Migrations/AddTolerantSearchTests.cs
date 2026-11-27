using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration of the tolerant search: existing artists, albums and tracks keep their data and get their search keys
/// from the upgrade the migration leaves to the service; reverting it removes the keys and the aliases again.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AddTolerantSearchTests
{
    private const string PreviousMigration = "20261127020747_AddArtistCredits";
    private const string Migration = "20261127193547_AddTolerantSearch";
    private const string Source = "019a0000-0000-7000-8000-000000000001";

    private static readonly Guid _pink = Guid.Parse("0000000b-0000-0000-0000-000000000001");
    private static readonly Guid _funhouse = Guid.Parse("0000000c-0000-0000-0000-000000000001");

    [Test]
    public async Task Migrate_ExistingLibrary_GetsItsSearchKeysFromThePendingUpgrade()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_tolerant_search",
            PreviousMigration);
        await SeedAsync(database);

        await database.MigrateAsync(Migration);

        await using (WaveMpDbContext db = database.CreateDbContext())
        {
            (await db.Artists.SingleAsync()).SearchKey.Should().BeEmpty();
            (await db.DataUpgrades.SingleAsync(upgrade => upgrade.Name == DataUpgrade.SearchKeys)).CompletedAt
                .Should().BeNull();
        }

        await using (WaveMpDbContext db = database.CreateDbContext())
        {
            await new SearchKeyUpgradeRepository(db).ApplyAsync(DateTimeOffset.UtcNow);
        }

        await using WaveMpDbContext read = database.CreateDbContext();
        (await read.Artists.SingleAsync()).SearchKey.Should().Be("pnk pink");
        (await read.Albums.SingleAsync()).SearchKey.Should().Be("funhouse");
        (await read.Tracks.Select(track => track.SearchKey).SingleAsync()).Should().Be("sowhat");
    }

    [Test]
    public async Task Revert_MigratedLibrary_KeepsTheLibrary()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_tolerant_search_down",
            PreviousMigration);
        await SeedAsync(database);
        await database.MigrateAsync(Migration);

        await database.MigrateAsync(PreviousMigration);

        await using WaveMpDbContext db = database.CreateDbContext();
        (await db.Database.SqlQueryRaw<string>("SELECT name AS \"Value\" FROM artists").SingleAsync())
            .Should().Be("P!nk");
        (await db.Database
                .SqlQueryRaw<string>("SELECT name AS \"Value\" FROM data_upgrades WHERE name = 'search-keys'")
                .ToListAsync())
            .Should().BeEmpty();
        (await db.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT table_name AS "Value" FROM information_schema.columns
                    WHERE column_name = 'search_key' OR table_name = 'artist_aliases'
                    """)
                .ToListAsync())
            .Should().BeEmpty();
    }

    private static async Task SeedAsync(TestDatabase database)
    {
        await using WaveMpDbContext dbContext = database.CreateDbContext();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO artists (id, name, normalized_name, first_seen_at)
            VALUES ({_pink}, 'P!nk', 'pnk', now());
            INSERT INTO albums (id, title, normalized_title, album_key, album_artist_id, is_compilation, first_seen_at,
                updated_at)
            VALUES ({_funhouse}, 'Funhouse', 'funhouse', 'artist:pnk|funhouse', {_pink}, false, now(), now());
            INSERT INTO tracks (id, directory_path, base_name, normalized_base_name, title, artist_id, album_id,
                has_audio, has_video, metadata_source, first_seen_at, updated_at, video_offset_milliseconds,
                source_id)
            VALUES (gen_random_uuid(), 'Music', 'So What', 'SO WHAT', 'So What', {_pink}, {_funhouse}, true, false,
                'Tags', now(), now(), 0, {Guid.Parse(Source)});
            """);
    }
}
