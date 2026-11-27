using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration that adds the media sources: the media folder of an existing installation becomes the first source,
/// and the whole library belongs to it.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AddMediaSourcesTests
{
    private const string PreviousMigration = "20261118134057_AddThemeSettings";

    [Test]
    public async Task Migrate_ExistingLibrary_BelongsToTheFirstSourceThatFollowsMediaPath()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_media_sources",
            PreviousMigration);
        var trackId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        await using (WaveMpDbContext dbContext = database.CreateDbContext())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO tracks (id, directory_path, base_name, normalized_base_name, title, has_audio, has_video,
                    metadata_source, first_seen_at, updated_at, video_offset_milliseconds)
                VALUES ({trackId}, 'Band', 'Song', 'SONG', 'Song', true, false, 'Tags', now(), now(), 0)
                """);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO media_files (id, relative_path, directory_path, file_name, base_name, normalized_base_name,
                    extension, kind, size, modified_at, track_id, is_primary, is_lossless, has_embedded_picture,
                    is_writable, metadata_version, first_seen_at, scanned_at)
                VALUES ({fileId}, 'Band/Song.mp3', 'Band', 'Song.mp3', 'Song', 'SONG', 'mp3', 'Audio', 100, now(),
                    {trackId}, true, false, false, true, 1, now(), now())
                """);
        }

        await database.MigrateAsync();

        await using WaveMpDbContext migrated = database.CreateDbContext();
        MediaSource source = await migrated.MediaSources.SingleAsync();
        source.Id.Should().Be(MediaSource.DefaultId);
        source.Path.Should().BeNull("the first source follows MEDIA_PATH");
        source.IsActive.Should().BeTrue();
        source.NormalizedName.Should().Be(source.Name.ToUpperInvariant());
        (await migrated.Tracks.SingleAsync()).SourceId.Should().Be(MediaSource.DefaultId);
        MediaFile file = await migrated.MediaFiles.SingleAsync();
        (file.Id, file.SourceId).Should().Be((fileId, MediaSource.DefaultId));
    }

    [Test]
    public async Task Migrate_EmptyDatabase_CreatesTheFirstSource()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_media_sources_empty");

        await using WaveMpDbContext dbContext = database.CreateDbContext();
        (await dbContext.MediaSources.Select(source => source.Id).ToListAsync())
            .Should().Equal(MediaSource.DefaultId);
    }
}
