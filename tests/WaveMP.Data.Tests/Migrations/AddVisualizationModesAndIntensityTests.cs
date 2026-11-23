using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Migrations;

/// <summary>
/// The migration that adds the color schemes, the intensity and the Milkdrop speed of the visualizations: the former
/// presets of A-Force and BlackCap become their color schemes, Milkdrop keeps its preset, and every profile gets the
/// defaults of a new profile for the intensity and the speed.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AddVisualizationModesAndIntensityTests
{
    private const string PreviousMigration = "20261122223656_AddAlbumMoves";

    [Test]
    public async Task Migrate_ExistingSettings_MoveThePresetsOfTheOwnModulesIntoTheColorSchemes()
    {
        await using TestDatabase database = await TestDatabase.CreateMigratedAsync(
            "wavemp_tests_migration_visualization_modes",
            PreviousMigration);
        var aforce = Guid.NewGuid();
        var blackCap = Guid.NewGuid();
        var milkdrop = Guid.NewGuid();
        var withoutPreset = Guid.NewGuid();
        await using (WaveMpDbContext dbContext = database.CreateDbContext())
        {
            foreach ((Guid id, string module, string? preset) in new[]
                     {
                         (aforce, "AForce", "ember"),
                         (blackCap, "BlackCap", "tide"),
                         (milkdrop, "Milkdrop", "Waves"),
                         (withoutPreset, "AForce", (string?)null),
                     })
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO users (id, name, normalized_name, initials, color, role, is_built_in, created_at)
                    VALUES ({id}, {id.ToString()}, {id.ToString()}, 'AB', '#1f6fd6', 'User', false, now())
                    """);
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO user_settings (user_id, language, visualization_module, visualization_preset)
                    VALUES ({id}, 'en', {module}, {preset})
                    """);
            }
        }

        await database.MigrateAsync();

        await using WaveMpDbContext migrated = database.CreateDbContext();
        Dictionary<Guid, UserSettings> settings = await migrated.Set<UserSettings>()
            .ToDictionaryAsync(row => row.UserId);
        settings[aforce].Should().BeEquivalentTo(new
        {
            VisualizationModule = VisualizationModule.AForce,
            VisualizationPreset = (string?)null,
            VisualizationColorScheme = "ember",
        });
        settings[blackCap].Should().BeEquivalentTo(new
        {
            VisualizationModule = VisualizationModule.BlackCap,
            VisualizationPreset = (string?)null,
            VisualizationColorScheme = "tide",
        });
        settings[milkdrop].Should().BeEquivalentTo(new
        {
            VisualizationModule = VisualizationModule.Milkdrop,
            VisualizationPreset = "Waves",
            VisualizationColorScheme = (string?)null,
        });
        settings[withoutPreset].VisualizationColorScheme.Should().BeNull();
        settings.Values.Should().AllSatisfy(row =>
        {
            row.VisualizationIntensity.Should().Be(VisualizationIntensity.Normal);
            row.VisualizationMilkdropSpeed.Should().Be(50);
        });
    }
}
