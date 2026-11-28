using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration of the song analysis: the offsets of videos that admins set become the corrected start of the song in
/// the video, in place, before the old column is removed; every other row of the library and of the profiles stays as
/// it was, the new setting to skip intro and outro is on for every profile and the table of the analyses starts empty.
/// Reverting it copies the corrected starts back into the offsets.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AddSongAnalysisTests
{
    private const string PreviousMigration = "20261127225106_AddLyricsMatcherVersion";
    private const string Migration = "20261128023106_AddSongAnalysis";
    private const string Source = "019a0000-0000-7000-8000-000000000001";

    private static readonly Guid _anna = Guid.Parse("0000000a-0000-0000-0000-000000000001");
    private static readonly Guid _ben = Guid.Parse("0000000a-0000-0000-0000-000000000002");
    private static readonly Guid _kflay = Guid.Parse("0000000b-0000-0000-0000-000000000001");
    private static readonly Guid _kda = Guid.Parse("0000000b-0000-0000-0000-000000000002");
    private static readonly Guid _acdc = Guid.Parse("0000000b-0000-0000-0000-000000000003");
    private static readonly Guid _beyonce = Guid.Parse("0000000b-0000-0000-0000-000000000004");
    private static readonly Guid _guns = Guid.Parse("0000000b-0000-0000-0000-000000000005");
    private static readonly Guid _simon = Guid.Parse("0000000b-0000-0000-0000-000000000006");
    private static readonly Guid _various = Guid.Parse("0000000b-0000-0000-0000-000000000007");
    private static readonly Guid _solutions = Guid.Parse("0000000c-0000-0000-0000-000000000001");
    private static readonly Guid _hits = Guid.Parse("0000000c-0000-0000-0000-000000000002");
    private static readonly Guid _playlist = Guid.Parse("0000000d-0000-0000-0000-000000000001");

    [Test]
    public async Task Migrate_Library_KeepsTheOffsetsAsCorrectedStartsAndEverythingElse()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_song_analysis",
            PreviousMigration);
        await SeedAsync(database);
        Snapshot before = await SnapshotAsync(database);
        string offsets = await QueryAsync(
            database,
            """
            SELECT string_agg(title || '=' || video_offset_milliseconds, ',' ORDER BY title) AS "Value"
            FROM tracks WHERE video_offset_milliseconds <> 0
            """);

        await database.MigrateAsync();

        (await SnapshotAsync(database)).Should().Be(before);
        (await QueryAsync(
                database,
                """
                SELECT string_agg(title || '=' || video_song_start_milliseconds, ',' ORDER BY title) AS "Value"
                FROM tracks WHERE video_song_start_milliseconds IS NOT NULL
                """))
            .Should().Be(offsets)
            .And.Be("Blood in the Cut=2500,Paradise City=600000,POP/STARS=-1200,The Boxer=-600000");
        (await CountAsync(
                database,
                """
                SELECT count(*)::int AS "Value" FROM tracks
                WHERE audio_song_start_milliseconds IS NOT NULL OR audio_song_end_milliseconds IS NOT NULL
                    OR video_song_end_milliseconds IS NOT NULL
                """))
            .Should().Be(0);
        (await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM media_file_analyses")).Should().Be(0);
        (await CountAsync(
                database,
                "SELECT count(*)::int AS \"Value\" FROM user_settings WHERE NOT video_skip_intro_and_outro"))
            .Should().Be(0);
        (await ColumnsAsync(database, "tracks", "video_offset_milliseconds")).Should().BeEmpty();
    }

    [Test]
    public async Task Migrate_AnalysisOfARemovedFile_IsRemovedWithIt()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_song_analysis_cascade",
            PreviousMigration);
        await SeedAsync(database);
        await database.MigrateAsync();
        await using WaveMpDbContext db = database.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO media_file_analyses (media_file_id, version, file_size, file_modified_at, analyzed_at, status,
                method, confidence, mismatch, pieces, loudness_lufs)
            SELECT id, 1, size, modified_at, now(), 'Analyzed', 'Alignment', 0.9, false,
                '[{{"fileStartMilliseconds":4000,"songStartMilliseconds":0,"durationMilliseconds":180000}}]', -9.5
            FROM media_files;
            """);
        int files = await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM media_files");

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM media_files WHERE relative_path = 'Music/Thunderstruck.mkv'");

        (await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM media_file_analyses"))
            .Should().Be(files - 1);
        (await db.MediaFileAnalyses.Where(analysis => analysis.Pieces.Count == 1).CountAsync())
            .Should().Be(files - 1);
    }

    [Test]
    public async Task Revert_MigratedLibrary_CopiesTheCorrectedStartsBackIntoTheOffsets()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_song_analysis_down",
            PreviousMigration);
        await SeedAsync(database);
        Snapshot before = await SnapshotAsync(database);
        await database.MigrateAsync(Migration);
        await using (WaveMpDbContext db = database.CreateDbContext())
        {
            // Corrections made after the update: a start beyond the former limits, and an audio correction.
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE tracks SET video_song_start_milliseconds = 700000 WHERE title = 'Paradise City';
                UPDATE tracks SET video_song_start_milliseconds = 900, audio_song_start_milliseconds = 150
                WHERE title = 'Thunderstruck';
                """);
        }

        await database.MigrateAsync(PreviousMigration);

        (await SnapshotAsync(database)).Should().Be(before);
        (await QueryAsync(
                database,
                """
                SELECT string_agg(title || '=' || video_offset_milliseconds, ',' ORDER BY title) AS "Value"
                FROM tracks WHERE video_offset_milliseconds <> 0
                """))
            .Should().Be(
                "Blood in the Cut=2500,Paradise City=600000,POP/STARS=-1200,The Boxer=-600000,Thunderstruck=900");
        (await ColumnsAsync(database, "tracks", "%song_%")).Should().BeEmpty();
        (await ColumnsAsync(database, "user_settings", "video_skip_intro_and_outro")).Should().BeEmpty();
        (await CountAsync(
                database,
                """
                SELECT count(*)::int AS "Value" FROM information_schema.tables
                WHERE table_name = 'media_file_analyses'
                """))
            .Should().Be(0);
    }

    /// <summary>
    /// A library as the previous version stored it: artists whose names need normalizing (K.Flay, K/DA, AC/DC,
    /// Beyonce with accent, Guns N' Roses, Simon &amp; Garfunkel), featurings, an album with album artist and a
    /// compilation, songs with audio file and video, videos with offsets in both directions and at the limits,
    /// cover rules; two profiles with settings, favorites, ratings, plays, a playlist, a smart playlist, an artist
    /// picture, an alias and a lyrics download.
    /// </summary>
    private static async Task SeedAsync(TestDatabase database)
    {
        await using WaveMpDbContext dbContext = database.CreateDbContext();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO artists (id, name, normalized_name, first_seen_at)
            VALUES ({_kflay}, 'K.Flay', 'kflay', now()), ({_kda}, 'K/DA', 'kda', now()),
                ({_acdc}, 'AC/DC', 'acdc', now()), ({_beyonce}, 'Beyoncé', 'beyonce', now()),
                ({_guns}, 'Guns N'' Roses', 'gunsnroses', now()),
                ({_simon}, 'Simon & Garfunkel', 'simongarfunkel', now()),
                ({_various}, 'Various Artists', 'variousartists', now());
            INSERT INTO albums (id, title, normalized_title, album_key, album_artist_id, is_compilation, first_seen_at,
                updated_at, cover_fallback, cover_priority)
            VALUES ({_solutions}, 'Solutions', 'solutions', 'artist:kflay|solutions', {_kflay}, false, now(), now(),
                    'ArtistPicture', 'TrackFirst'),
                ({_hits}, 'Hits', 'hits', 'artist:variousartists|hits', {_various}, true, now(), now(), NULL, NULL);
            INSERT INTO tracks (id, directory_path, base_name, normalized_base_name, title, artist_id, album_id,
                has_audio, has_video, metadata_source, first_seen_at, updated_at, video_offset_milliseconds,
                source_id, cover_priority)
            SELECT md5(t.title)::uuid, 'Music', replace(t.title, '/', '_'), upper(replace(t.title, '/', '_')), t.title,
                t.artist, t.album, true, t.video, 'Tags', now(), now(), t.offset_ms, {Guid.Parse(Source)}, t.cover
            FROM (VALUES ('Blood in the Cut', {_kflay}, {_solutions}, true, 2500, 'AlbumFirst'),
                ('High Enough', {_kflay}, {_solutions}, true, 0, NULL),
                ('POP/STARS', {_kda}, {_hits}, true, -1200, NULL),
                ('Thunderstruck', {_acdc}, {_hits}, true, 0, NULL),
                ('Halo', {_beyonce}, {_hits}, false, 0, NULL),
                ('Paradise City', {_guns}, NULL::uuid, true, 600000, NULL),
                ('The Boxer', {_simon}, NULL::uuid, true, -600000, 'TrackFirst'),
                ('Unknown', NULL::uuid, NULL::uuid, false, 0, NULL))
                AS t(title, artist, album, video, offset_ms, cover);
            INSERT INTO media_files (id, relative_path, directory_path, file_name, base_name, normalized_base_name,
                extension, kind, size, modified_at, track_id, is_primary, is_lossless, has_embedded_picture,
                is_writable, metadata_version, first_seen_at, scanned_at, source_id, tags_artist)
            SELECT gen_random_uuid(), 'Music/' || base_name || '.' || f.extension, 'Music',
                base_name || '.' || f.extension, base_name, normalized_base_name, f.extension, f.kind, 1000, now(), id,
                true, false, false, f.kind = 'Audio', 3, now(), now(), {Guid.Parse(Source)}, title
            FROM tracks CROSS JOIN (VALUES ('mp3', 'Audio'), ('mkv', 'Video')) AS f(extension, kind)
            WHERE f.kind = 'Audio' OR has_video;
            INSERT INTO users (id, name, normalized_name, initials, color, role, is_built_in, created_at)
            VALUES ({_anna}, 'Anna', 'ANNA', 'A', 'blue', 'User', false, now()),
                ({_ben}, 'Ben', 'BEN', 'B', 'green', 'User', false, now());
            INSERT INTO user_settings (user_id, language, video_ambient_mode)
            VALUES ({_anna}, 'de', false), ({_ben}, 'en', true);
            INSERT INTO favorites (id, user_id, type, artist_id, created_at)
            VALUES (gen_random_uuid(), {_anna}, 'Artist', {_kflay}, now()),
                (gen_random_uuid(), {_ben}, 'Artist', {_guns}, now());
            INSERT INTO favorites (id, user_id, type, album_id, created_at)
            VALUES (gen_random_uuid(), {_anna}, 'Album', {_hits}, now());
            INSERT INTO favorites (id, user_id, type, track_id, created_at)
            SELECT gen_random_uuid(), {_ben}, 'Track', id, now() FROM tracks WHERE title IN ('The Boxer', 'Halo');
            INSERT INTO track_ratings (user_id, track_id, rating, updated_at)
            SELECT u.id, t.id, 4, now() FROM tracks t CROSS JOIN users u WHERE u.id IN ({_anna}, {_ben});
            INSERT INTO play_history (id, user_id, track_id, played_at)
            SELECT gen_random_uuid(), {_anna}, id, now() - interval '1 day' FROM tracks;
            INSERT INTO playlists (id, user_id, name, normalized_name, created_at, updated_at)
            VALUES ({_playlist}, {_ben}, 'Road Trip', 'ROAD TRIP', now(), now());
            INSERT INTO playlist_entries (id, playlist_id, track_id, position, added_at)
            SELECT gen_random_uuid(), {_playlist}, id, row_number() OVER (ORDER BY title) - 1, now() FROM tracks;
            INSERT INTO smart_playlists (id, user_id, name, normalized_name, match, rules, sort, direction,
                created_at, updated_at)
            VALUES (gen_random_uuid(), {_anna}, 'K-Pop', 'K-POP', 'All',
                jsonb_build_array(jsonb_build_object('field', 'Artist', 'operator', 'Is', 'artistId', {_kda}::text)),
                'Title',
                'Ascending', now(), now());
            INSERT INTO artist_pictures (id, artist_id, kind, created_at, width, height)
            VALUES (gen_random_uuid(), {_acdc}, 'Uploaded', now(), 600, 600);
            INSERT INTO artist_aliases (id, artist_id, name, normalized_name, search_key, source, is_removed,
                created_at)
            VALUES (gen_random_uuid(), {_guns}, 'GNR', 'gnr', 'gnr', 'Manual', false, now());
            INSERT INTO lyrics_downloads (track_id, status, checked_at)
            SELECT id, 'NotFound', now() FROM tracks WHERE title = 'Halo';
            """);
    }

    /// <summary>
    /// Counts and relations of everything the migration must keep, in columns both versions have.
    /// </summary>
    private static async Task<Snapshot> SnapshotAsync(TestDatabase database)
    {
        return new Snapshot(
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM artists"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM albums"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM tracks"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM media_files"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM users"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM user_settings"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM favorites"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM track_ratings"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM play_history"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM playlist_entries"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM smart_playlists"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM artist_pictures"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM artist_aliases"),
            await CountAsync(database, "SELECT count(*)::int AS \"Value\" FROM lyrics_downloads"),
            await QueryAsync(
                database,
                """
                SELECT string_agg(t.title || '=' || coalesce(a.name, '-') || '/' || coalesce(al.title, '-') || '/'
                    || coalesce(t.cover_priority, '-') || '/' || t.has_video, ',' ORDER BY t.title) AS "Value"
                FROM tracks t LEFT JOIN artists a ON a.id = t.artist_id LEFT JOIN albums al ON al.id = t.album_id
                """),
            await QueryAsync(
                database,
                """
                SELECT string_agg(al.title || '=' || coalesce(a.name, '-') || '/' || coalesce(al.cover_priority, '-')
                    || '/' || coalesce(al.cover_fallback, '-'), ',' ORDER BY al.title) AS "Value"
                FROM albums al LEFT JOIN artists a ON a.id = al.album_artist_id
                """),
            await QueryAsync(
                database,
                """
                SELECT string_agg(f.relative_path || '=' || t.title || '/' || f.kind || '/' || f.is_primary, ','
                    ORDER BY f.relative_path) AS "Value"
                FROM media_files f JOIN tracks t ON t.id = f.track_id
                """),
            await QueryAsync(
                database,
                """
                SELECT string_agg(target, ',' ORDER BY target) AS "Value"
                FROM (SELECT f.user_id || ':' || f.type || ':'
                    || coalesce(f.artist_id::text, f.album_id::text, f.track_id::text) AS target
                    FROM favorites f) AS targets
                """),
            await QueryAsync(
                database,
                """
                SELECT string_agg(u.name || '=' || s.language || '/' || s.video_ambient_mode, ',' ORDER BY u.name)
                    AS "Value"
                FROM user_settings s JOIN users u ON u.id = s.user_id
                """),
            await QueryAsync(
                database,
                "SELECT string_agg(rules::text, ',' ORDER BY name) AS \"Value\" FROM smart_playlists"));
    }

    private static async Task<int> CountAsync(TestDatabase database, string sql)
    {
        await using WaveMpDbContext db = database.CreateDbContext();
        return await db.Database.SqlQueryRaw<int>(sql).SingleAsync();
    }

    private static async Task<string> QueryAsync(TestDatabase database, string sql)
    {
        await using WaveMpDbContext db = database.CreateDbContext();
        return await db.Database.SqlQueryRaw<string>(sql).SingleAsync();
    }

    private static async Task<List<string>> ColumnsAsync(TestDatabase database, string table, string pattern)
    {
        await using WaveMpDbContext db = database.CreateDbContext();
        return await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_name = {0} AND column_name LIKE {1}
                """,
                table,
                pattern)
            .ToListAsync();
    }

    private sealed record Snapshot(
        int Artists,
        int Albums,
        int Tracks,
        int MediaFiles,
        int Users,
        int UserSettings,
        int Favorites,
        int Ratings,
        int Plays,
        int PlaylistEntries,
        int SmartPlaylists,
        int ArtistPictures,
        int ArtistAliases,
        int LyricsDownloads,
        string TrackRelations,
        string AlbumsWithCoverRules,
        string Files,
        string FavoriteTargets,
        string Settings,
        string SmartPlaylistRules);
}
