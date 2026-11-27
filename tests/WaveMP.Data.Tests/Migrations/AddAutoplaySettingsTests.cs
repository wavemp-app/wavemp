using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration that adds the autoplay settings: existing profiles get autoplay on, with songs that only have a music
/// video, like new profiles.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AddAutoplaySettingsTests
{
    private const string PreviousMigration = "20261119032853_AddMediaSources";

    [Test]
    public async Task Migrate_ExistingSettings_TurnAutoplayOn()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_data_migration_autoplay",
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
            AutoplayEnabled = true,
            AutoplayIncludeVideoOnly = true,
            Language = "de",
            CrossfadeSeconds = 7,
        });
    }
}
