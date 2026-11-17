using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration that stores the language as BCP 47 tag: the names of the former enum become tags, so every profile
/// keeps its language.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ChangeLanguageToTagTests
{
    private const string PreviousMigration = "20261117155002_AddLyricsDownloads";

    [Test]
    public async Task Migrate_ExistingSettings_KeepTheLanguageOfEachProfileAsTag()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_migration_language_tag",
            PreviousMigration);
        var english = Guid.NewGuid();
        var german = Guid.NewGuid();
        await using (WaveMpDbContext dbContext = database.CreateDbContext())
        {
            foreach ((Guid id, string language) in new[] { (english, "En"), (german, "De") })
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO users (id, name, normalized_name, initials, color, role, is_built_in, created_at)
                    VALUES ({id}, {id.ToString()}, {id.ToString()}, 'AB', '#1f6fd6', 'User', false, now())
                    """);
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO user_settings (user_id, language) VALUES ({id}, {language})");
            }
        }

        await database.MigrateAsync();

        await using WaveMpDbContext migrated = database.CreateDbContext();
        Dictionary<Guid, string> languages = await migrated.Set<UserSettings>()
            .ToDictionaryAsync(row => row.UserId, row => row.Language);
        languages.Should().Equal(new Dictionary<Guid, string> { [english] = "en", [german] = "de" });
    }
}
