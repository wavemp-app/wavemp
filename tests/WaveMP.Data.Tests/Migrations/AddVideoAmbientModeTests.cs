using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration that adds the ambient light of videos: existing profiles get it on, like new profiles.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AddVideoAmbientModeTests
{
    private const string PreviousMigration = "20261120172757_AddRecapShares";

    [Test]
    public async Task Migrate_ExistingSettings_TurnTheAmbientModeOn()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_video_ambient",
            PreviousMigration);
        var userId = Guid.NewGuid();
        await using (WaveMpDbContext dbContext = database.CreateDbContext())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO users (id, name, normalized_name, initials, color, role, is_built_in, created_at)
                VALUES ({userId}, {userId.ToString()}, {userId.ToString()}, 'AB', '#1f6fd6', 'User', false, now())
                """);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO user_settings (user_id, language, crossfade_seconds) VALUES ({userId}, 'de', 7)");
        }

        await database.MigrateAsync();

        await using WaveMpDbContext migrated = database.CreateDbContext();
        UserSettings settings = await migrated.Set<UserSettings>().SingleAsync(row => row.UserId == userId);
        settings.Should().BeEquivalentTo(new
        {
            VideoAmbientMode = true,
            Language = "de",
            CrossfadeSeconds = 7,
        });
    }
}
