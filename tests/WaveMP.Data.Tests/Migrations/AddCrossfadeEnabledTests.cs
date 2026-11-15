using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration that adds the switch of the crossfade: profiles with a crossfade keep it, the others get the switch
/// off and the default length, so turning it on fades.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AddCrossfadeEnabledTests
{
    private const string PreviousMigration = "20261112045459_AddPlaybackEnhancements";

    [Test]
    public async Task Migrate_ExistingSettings_KeepTheCrossfadeOfEachProfile()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_migration_crossfade",
            PreviousMigration);
        var withCrossfade = Guid.NewGuid();
        var withoutCrossfade = Guid.NewGuid();
        await using (WaveMpDbContext dbContext = database.CreateDbContext())
        {
            foreach ((Guid id, int seconds) in new[] { (withCrossfade, 7), (withoutCrossfade, 0) })
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO users (id, name, normalized_name, initials, color, role, is_built_in, created_at)
                    VALUES ({id}, {id.ToString()}, {id.ToString()}, 'AB', '#1f6fd6', 'User', false, now())
                    """);
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO user_settings (user_id, language, crossfade_seconds) VALUES ({id}, 'En', {seconds})");
            }
        }

        await database.MigrateAsync();

        await using WaveMpDbContext migrated = database.CreateDbContext();
        Dictionary<Guid, UserSettings> settings = await migrated.Set<UserSettings>()
            .ToDictionaryAsync(row => row.UserId);
        settings[withCrossfade].Should().BeEquivalentTo(new { CrossfadeEnabled = true, CrossfadeSeconds = 7 });
        settings[withoutCrossfade].Should().BeEquivalentTo(new { CrossfadeEnabled = false, CrossfadeSeconds = 5 });
    }
}
