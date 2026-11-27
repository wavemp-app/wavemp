using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration that gives songs and albums several artists: every song keeps its artist and every album its album
/// artist as their only artist, the files get empty lists of artist values, and nothing of the library or the users
/// changes. Reverting it removes the lists again and keeps the artist of each song and album.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AddArtistCreditsTests
{
    private const string PreviousMigration = "20261126202032_ReferenceGenresByIdAndNormalizeNames";
    private const string Migration = "20261127020747_AddArtistCredits";
    private const string Source = "019a0000-0000-7000-8000-000000000001";

    private static readonly Guid _anna = Guid.Parse("0000000a-0000-0000-0000-000000000001");
    private static readonly Guid _ben = Guid.Parse("0000000a-0000-0000-0000-000000000002");
    private static readonly Guid _kflay = Guid.Parse("0000000b-0000-0000-0000-000000000001");
    private static readonly Guid _acdc = Guid.Parse("0000000b-0000-0000-0000-000000000002");
    private static readonly Guid _simon = Guid.Parse("0000000b-0000-0000-0000-000000000003");
    private static readonly Guid _featuring = Guid.Parse("0000000b-0000-0000-0000-000000000004");
    private static readonly Guid _various = Guid.Parse("0000000b-0000-0000-0000-000000000005");
    private static readonly Guid _solutions = Guid.Parse("0000000c-0000-0000-0000-000000000001");
    private static readonly Guid _hits = Guid.Parse("0000000c-0000-0000-0000-000000000002");
    private static readonly Guid _compilation = Guid.Parse("0000000c-0000-0000-0000-000000000003");
    private static readonly Guid _playlist = Guid.Parse("0000000d-0000-0000-0000-000000000001");

    [Test]
    public async Task Migrate_SongsAndAlbums_KeepTheirArtistAsOnlyArtistAndTheUserDataStays()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_artist_credits",
            PreviousMigration);
        await SeedAsync(database);
        Counts before = await CountAsync(database);

        await database.MigrateAsync();

        await using WaveMpDbContext db = database.CreateDbContext();
        var trackCredits = await db.TrackArtistCredits
            .Select(credit => new { credit.Track!.Title, credit.ArtistId, credit.Position, credit.Role })
            .ToListAsync();
        trackCredits.Select(credit => (credit.Title, credit.ArtistId, credit.Position, credit.Role))
            .Should().BeEquivalentTo([
                ("Blood in the Cut", _kflay, 0, ArtistRole.Main),
                ("High Enough", _kflay, 0, ArtistRole.Main),
                ("Thunderstruck", _acdc, 0, ArtistRole.Main),
                ("The Boxer", _simon, 0, ArtistRole.Main),
                ("Rock This Party", _featuring, 0, ArtistRole.Main),
            ]);
        (await db.Tracks.Where(track => track.ArtistId == null).Select(track => track.Title).ToListAsync())
            .Should().Equal("Unknown");
        var albumCredits = await db.AlbumArtistCredits
            .Select(credit => new { credit.AlbumId, credit.ArtistId, credit.Position })
            .ToListAsync();
        albumCredits.Select(credit => (credit.AlbumId, credit.ArtistId, credit.Position))
            .Should().BeEquivalentTo([(_solutions, _kflay, 0), (_hits, _various, 0)]);
        (await db.MediaFiles.Select(file => new
        {
            file.Tags.ArtistValues,
            file.Tags.AlbumArtistValues,
            file.Tags.MusicBrainzArtistIds,
            file.Tags.MusicBrainzAlbumArtistIds,
        })
            .ToListAsync())
            .Should().HaveCount(before.MediaFiles).And.OnlyContain(tags => tags.ArtistValues.Count == 0
                && tags.AlbumArtistValues.Count == 0
                && tags.MusicBrainzArtistIds.Count == 0
                && tags.MusicBrainzAlbumArtistIds.Count == 0);
        (await CountAsync(database)).Should().Be(before);
        (await db.Tracks.Where(track => track.Title == "Rock This Party").Select(track => track.Artist!.Name)
                .SingleAsync())
            .Should().Be("Bob Sinclar feat. K.Flay");
    }

    [Test]
    public async Task Revert_MigratedLibrary_DropsTheListsAndKeepsEverythingElse()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_artist_credits_down",
            PreviousMigration);
        await SeedAsync(database);
        Counts before = await CountAsync(database);
        await database.MigrateAsync(Migration);

        await database.MigrateAsync(PreviousMigration);

        (await CountAsync(database)).Should().Be(before);
        await using WaveMpDbContext db = database.CreateDbContext();
        List<string> tables = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT table_name AS "Value" FROM information_schema.tables
                WHERE table_name IN ('track_artist_credits', 'album_artist_credits')
                """)
            .ToListAsync();
        tables.Should().BeEmpty();
        List<string> columns = await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_name = 'media_files' AND column_name LIKE 'tags_%artist%'
                """)
            .ToListAsync();
        columns.Should().BeEquivalentTo(
            "tags_artist",
            "tags_artist_sort",
            "tags_album_artist",
            "tags_album_artist_sort",
            "tags_artists",
            "tags_music_brainz_artist_id",
            "tags_music_brainz_album_artist_id");
        (await db.Database
                .SqlQueryRaw<Guid>("SELECT artist_id AS \"Value\" FROM tracks WHERE title = 'Thunderstruck'")
                .SingleAsync())
            .Should().Be(_acdc);
    }

    /// <summary>
    /// A library as the previous version stored it: artists whose names must not be split (AC/DC, Simon &amp;
    /// Garfunkel), a featuring that was one artist so far, an album with album artist, a compilation of "Various
    /// Artists" and one without album artist, a song without artist; two users with favorites of songs, albums and
    /// artists, ratings, plays, a playlist and an artist picture.
    /// </summary>
    private static async Task SeedAsync(TestDatabase database)
    {
        await using WaveMpDbContext dbContext = database.CreateDbContext();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO artists (id, name, normalized_name, first_seen_at)
            VALUES ({_kflay}, 'K.Flay', 'kflay', now()), ({_acdc}, 'AC/DC', 'acdc', now()),
                ({_simon}, 'Simon & Garfunkel', 'simongarfunkel', now()),
                ({_featuring}, 'Bob Sinclar feat. K.Flay', 'bobsinclarfeatkflay', now()),
                ({_various}, 'Various Artists', 'variousartists', now());
            INSERT INTO albums (id, title, normalized_title, album_key, album_artist_id, is_compilation, first_seen_at,
                updated_at, cover_fallback)
            VALUES ({_solutions}, 'Solutions', 'solutions', 'artist:kflay|solutions', {_kflay}, false, now(), now(),
                    'ArtistPicture'),
                ({_hits}, 'Hits', 'hits', 'artist:variousartists|hits', {_various}, true, now(), now(), NULL),
                ({_compilation}, 'Mixtape', 'mixtape', 'folder:mixtape', NULL, true, now(), now(), NULL);
            INSERT INTO tracks (id, directory_path, base_name, normalized_base_name, title, artist_id, album_id,
                has_audio, has_video, metadata_source, first_seen_at, updated_at, video_offset_milliseconds,
                source_id)
            SELECT md5(t.title)::uuid, 'Music', t.title, upper(t.title), t.title, t.artist, t.album, true, false,
                'Tags', now(), now(), 0, {Guid.Parse(Source)}
            FROM (VALUES ('Blood in the Cut', {_kflay}, {_solutions}), ('High Enough', {_kflay}, {_solutions}),
                ('Thunderstruck', {_acdc}, {_hits}), ('The Boxer', {_simon}, {_compilation}),
                ('Rock This Party', {_featuring}, NULL::uuid), ('Unknown', NULL::uuid, NULL::uuid))
                AS t(title, artist, album);
            INSERT INTO media_files (id, relative_path, directory_path, file_name, base_name, normalized_base_name,
                extension, kind, size, modified_at, track_id, is_primary, is_lossless, has_embedded_picture,
                is_writable, metadata_version, first_seen_at, scanned_at, source_id, tags_artist, tags_artists)
            SELECT gen_random_uuid(), 'Music/' || title || '.mp3', 'Music', title || '.mp3', title, upper(title),
                'mp3', 'Audio', 1000, now(), id, true, false, false, true, 3, now(), now(), {Guid.Parse(Source)},
                title, title || ' and friends'
            FROM tracks;
            INSERT INTO users (id, name, normalized_name, initials, color, role, is_built_in, created_at)
            VALUES ({_anna}, 'Anna', 'ANNA', 'A', 'blue', 'User', false, now()),
                ({_ben}, 'Ben', 'BEN', 'B', 'green', 'User', false, now());
            INSERT INTO favorites (id, user_id, type, artist_id, created_at)
            VALUES (gen_random_uuid(), {_anna}, 'Artist', {_kflay}, now()),
                (gen_random_uuid(), {_ben}, 'Artist', {_featuring}, now());
            INSERT INTO favorites (id, user_id, type, album_id, created_at)
            VALUES (gen_random_uuid(), {_anna}, 'Album', {_hits}, now());
            INSERT INTO favorites (id, user_id, type, track_id, created_at)
            SELECT gen_random_uuid(), {_ben}, 'Track', id, now() FROM tracks WHERE title = 'The Boxer';
            INSERT INTO track_ratings (user_id, track_id, rating, updated_at)
            SELECT u.id, t.id, 4, now() FROM tracks t CROSS JOIN users u WHERE u.id IN ({_anna}, {_ben});
            INSERT INTO play_history (id, user_id, track_id, played_at)
            SELECT gen_random_uuid(), {_anna}, id, now() - interval '1 day' FROM tracks;
            INSERT INTO playlists (id, user_id, name, normalized_name, created_at, updated_at)
            VALUES ({_playlist}, {_ben}, 'Road Trip', 'ROAD TRIP', now(), now());
            INSERT INTO playlist_entries (id, playlist_id, track_id, position, added_at)
            SELECT gen_random_uuid(), {_playlist}, id, row_number() OVER (ORDER BY title) - 1, now() FROM tracks;
            INSERT INTO artist_pictures (id, artist_id, kind, created_at, width, height)
            VALUES (gen_random_uuid(), {_acdc}, 'Uploaded', now(), 600, 600);
            """);
    }

    private static async Task<Counts> CountAsync(TestDatabase database)
    {
        await using WaveMpDbContext db = database.CreateDbContext();
        return new Counts(
            await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM artists").SingleAsync(),
            await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM albums").SingleAsync(),
            await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM tracks").SingleAsync(),
            await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM media_files").SingleAsync(),
            await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM favorites").SingleAsync(),
            await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM track_ratings").SingleAsync(),
            await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM play_history").SingleAsync(),
            await db.Database
                .SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM playlist_entries")
                .SingleAsync(),
            await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM artist_pictures").SingleAsync(),
            await db.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT string_agg(t.title || '=' || coalesce(a.name, '-'), ',' ORDER BY t.title) AS "Value"
                    FROM tracks t LEFT JOIN artists a ON a.id = t.artist_id
                    """)
                .SingleAsync(),
            await db.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT string_agg(al.title || '=' || coalesce(a.name, '-') || '/' || al.album_key, ','
                        ORDER BY al.title) AS "Value"
                    FROM albums al LEFT JOIN artists a ON a.id = al.album_artist_id
                    """)
                .SingleAsync(),
            await db.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT string_agg(target, ',' ORDER BY target) AS "Value"
                    FROM (SELECT f.user_id || ':' || f.type || ':'
                        || coalesce(f.artist_id::text, f.album_id::text, f.track_id::text) AS target
                        FROM favorites f) AS targets
                    """)
                .SingleAsync(),
            await db.Database
                .SqlQueryRaw<string>(
                    "SELECT string_agg(tags_artists, ',' ORDER BY id) AS \"Value\" FROM media_files")
                .SingleAsync());
    }

    private sealed record Counts(
        int Artists,
        int Albums,
        int Tracks,
        int MediaFiles,
        int Favorites,
        int Ratings,
        int Plays,
        int PlaylistEntries,
        int ArtistPictures,
        string TrackArtists,
        string AlbumArtists,
        string FavoriteTargets,
        string FileArtists);
}
