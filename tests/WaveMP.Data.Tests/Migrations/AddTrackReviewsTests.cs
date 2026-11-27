using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration that adds the review status: tracks that existed before start unreviewed, so the review inbox shows
/// the whole library.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AddTrackReviewsTests
{
    private const string PreviousMigration = "20261117174303_ChangeLanguageToTag";

    [Test]
    public async Task Migrate_ExistingTracks_StartUnreviewed()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_track_reviews",
            PreviousMigration);
        var trackId = Guid.NewGuid();
        await using (WaveMpDbContext dbContext = database.CreateDbContext())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO tracks (id, directory_path, base_name, normalized_base_name, title, has_audio, has_video,
                    metadata_source, first_seen_at, updated_at, video_offset_milliseconds)
                VALUES ({trackId}, '', 'Song', 'SONG', 'Song', true, false, 'Tags', now(), now(), 0)
                """);
        }

        await database.MigrateAsync();

        await using WaveMpDbContext migrated = database.CreateDbContext();
        Track track = await migrated.Tracks.SingleAsync();
        track.Id.Should().Be(trackId);
        track.ReviewedAt.Should().BeNull();
        track.ReviewedByUserId.Should().BeNull();
    }
}
