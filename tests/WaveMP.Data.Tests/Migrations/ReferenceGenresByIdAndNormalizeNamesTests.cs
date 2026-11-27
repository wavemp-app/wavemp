using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration that stores the genre of a track by id: the genre texts of the tracks become genres (texts that
/// differ only in case or spaces at the ends are one genre), nothing else of the library or the users changes, and
/// the upgrade of the names is left pending for the service. The full upgrade is tested with the scanner
/// (NameKeyUpgraderDatabaseTests).
/// </summary>
[TestFixture]
[Category("Integration")]
public class ReferenceGenresByIdAndNormalizeNamesTests
{
    private const string PreviousMigration = "20261126093256_AddAlbumCoverFallback";
    private const string Migration = "20261126202032_ReferenceGenresByIdAndNormalizeNames";

    private static readonly Guid _user = Guid.Parse("0000000a-0000-0000-0000-000000000001");
    private static readonly Guid _artist = Guid.Parse("0000000b-0000-0000-0000-000000000001");
    private static readonly Guid _album = Guid.Parse("0000000c-0000-0000-0000-000000000001");

    [Test]
    public async Task Migrate_TracksWithGenreTexts_ReferenceGenresAndKeepTheUserData()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_genres",
            PreviousMigration);
        await SeedAsync(database);

        await database.MigrateAsync();

        await using WaveMpDbContext db = database.CreateDbContext();
        List<Genre> genres = await db.Genres.ToListAsync();
        genres.Select(genre => genre.Name).Should().BeEquivalentTo("Hip-Hop", "Rock", "hip hop");
        var tracks = await db.Tracks.OrderBy(track => track.Title)
            .Select(track => new { track.Title, Genre = track.Genre == null ? null : track.Genre.Name })
            .ToListAsync();
        tracks.Select(track => (track.Title, track.Genre)).Should().Equal(
            ("Five", null),
            ("Four", "hip hop"),
            ("One", "Rock"),
            ("Six", null),
            ("Three", "Hip-Hop"),
            ("Two", "Rock"));
        (await db.Favorites.CountAsync(), await db.TrackRatings.CountAsync(), await db.PlayHistory.CountAsync())
            .Should().Be((2, 6, 6));
        (await db.Artists.SingleAsync()).Name.Should().Be("K.Flay");
        DataUpgrade upgrade = await db.DataUpgrades.SingleAsync();
        (upgrade.Name, upgrade.CompletedAt).Should().Be((DataUpgrade.NameKeys, null));
    }

    [Test]
    public async Task Revert_MigratedTracks_GetTheirGenreTextsBack()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_genres_down",
            PreviousMigration);
        await SeedAsync(database);
        await database.MigrateAsync(Migration);

        await database.MigrateAsync(PreviousMigration);

        await using WaveMpDbContext db = database.CreateDbContext();
        List<string?> genres = await db.Database
            .SqlQueryRaw<string?>("SELECT genre AS \"Value\" FROM tracks ORDER BY title")
            .ToListAsync();
        genres.Should().Equal(null, "hip hop", "Rock", null, "Hip-Hop", "Rock");
        (await db.Database.SqlQueryRaw<string>("SELECT normalized_name AS \"Value\" FROM artists").SingleAsync())
            .Should().Be("K.FLAY");
        (await db.Favorites.CountAsync(), await db.TrackRatings.CountAsync(), await db.PlayHistory.CountAsync())
            .Should().Be((2, 6, 6));
    }

    private static async Task SeedAsync(TestDatabase database)
    {
        await using WaveMpDbContext dbContext = database.CreateDbContext();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO artists (id, name, normalized_name, first_seen_at)
            VALUES ({_artist}, 'K.Flay', 'K.FLAY', now());
            INSERT INTO albums (id, title, normalized_title, album_key, album_artist_id, is_compilation, first_seen_at,
                updated_at)
            VALUES ({_album}, 'Solutions', 'SOLUTIONS', 'old-key', {_artist}, false, now(), now());
            INSERT INTO tracks (id, directory_path, base_name, normalized_base_name, title, artist_id, album_id, genre,
                has_audio, has_video, metadata_source, first_seen_at, updated_at, video_offset_milliseconds,
                source_id)
            SELECT md5(t.title)::uuid, 'K.Flay/Solutions', t.title, upper(t.title), t.title, {_artist}, {_album},
                t.genre, true, false, 'Tags', now(), now(), 0, '019a0000-0000-7000-8000-000000000001'
            FROM (VALUES ('One', 'Rock'), ('Two', ' rock '), ('Three', 'Hip-Hop'), ('Four', 'hip hop'),
                ('Five', NULL), ('Six', '  ')) AS t(title, genre);
            INSERT INTO users (id, name, normalized_name, initials, color, role, is_built_in, created_at)
            VALUES ({_user}, 'Anna', 'ANNA', 'A', 'blue', 'User', false, now());
            INSERT INTO favorites (id, user_id, type, artist_id, created_at)
            VALUES (gen_random_uuid(), {_user}, 'Artist', {_artist}, now());
            INSERT INTO favorites (id, user_id, type, album_id, created_at)
            VALUES (gen_random_uuid(), {_user}, 'Album', {_album}, now());
            INSERT INTO track_ratings (user_id, track_id, rating, updated_at)
            SELECT {_user}, id, 4, now() FROM tracks;
            INSERT INTO play_history (id, user_id, track_id, played_at)
            SELECT gen_random_uuid(), {_user}, id, now() FROM tracks;
            """);
    }
}
