using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class UserSettingsRepositoryTests : LibraryDatabaseTests
{
    protected override string DatabaseName => "wavemp_tests_user_settings";

    [Test]
    public async Task UpdateAsync_PlaybackSettings_AreReadBackUnchanged()
    {
        User user = await AddUserAsync();
        IUserRepository repository = CreateRepository();
        User loaded = (await repository.FindAsync(user.Id))!;
        loaded.Settings = new UserSettings
        {
            UserId = user.Id,
            EqualizerEnabled = true,
            EqualizerPreset = EqualizerPreset.Custom,
            EqualizerPreampDb = -3.5,
            EqualizerCustomGainsDb = [6, 4.5, 3, 1, 0, -1, -2.5, 0, 2, 4],
            CrossfadeEnabled = true,
            CrossfadeSeconds = 7,
            CrossfadeAlbumsGapless = false,
            SleepTimerFadeSeconds = 30,
            VisualizationModule = VisualizationModule.AForce,
            VisualizationPreset = "Waves",
            VisualizationAutoChange = false,
            VisualizationAutoChangeSeconds = 90,
            VisualizationQuality = VisualizationQuality.High,
        };

        await repository.UpdateAsync(loaded);

        UserSettings settings = (await CreateRepository().FindAsync(user.Id))!.Settings!;
        settings.Should().BeEquivalentTo(new
        {
            EqualizerEnabled = true,
            EqualizerPreset = EqualizerPreset.Custom,
            EqualizerPreampDb = -3.5,
            EqualizerCustomGainsDb = new double[] { 6, 4.5, 3, 1, 0, -1, -2.5, 0, 2, 4 },
            CrossfadeEnabled = true,
            CrossfadeSeconds = 7,
            CrossfadeAlbumsGapless = false,
            SleepTimerFadeSeconds = 30,
            VisualizationModule = VisualizationModule.AForce,
            VisualizationPreset = "Waves",
            VisualizationAutoChange = false,
            VisualizationAutoChangeSeconds = 90,
            VisualizationQuality = VisualizationQuality.High,
        });
    }

    [Test]
    public async Task UpdateAsync_ChangedCustomGain_IsSaved()
    {
        User user = await AddUserAsync();
        IUserRepository repository = CreateRepository();
        User loaded = (await repository.FindAsync(user.Id))!;
        loaded.Settings = new UserSettings { UserId = user.Id };
        await repository.UpdateAsync(loaded);

        loaded.Settings.EqualizerCustomGainsDb = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        await repository.UpdateAsync(loaded);

        (await CreateRepository().FindAsync(user.Id))!.Settings!.EqualizerCustomGainsDb
            .Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    }

    [Test]
    public async Task FindAsync_RowWithoutPlaybackValues_ReadsTheDefaultsOfTheColumns()
    {
        User user = await AddUserAsync();
        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            // Like a row from before the migration: only the older columns are written.
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO user_settings (user_id, language) VALUES ({user.Id}, 'De')");
        }

        UserSettings settings = (await CreateRepository().FindAsync(user.Id))!.Settings!;

        settings.Should().BeEquivalentTo(new
        {
            Language = Language.De,
            SidebarCollapsed = false,
            EqualizerEnabled = false,
            EqualizerPreset = EqualizerPreset.Flat,
            EqualizerPreampDb = 0d,
            EqualizerCustomGainsDb = new double[10],
            CrossfadeEnabled = false,
            CrossfadeSeconds = 5,
            CrossfadeAlbumsGapless = true,
            SleepTimerFadeSeconds = 10,
            VisualizationModule = VisualizationModule.Milkdrop,
            VisualizationPreset = (string?)null,
            VisualizationAutoChange = true,
            VisualizationAutoChangeSeconds = 30,
            VisualizationQuality = VisualizationQuality.Auto,
        });
    }

    private UserRepository CreateRepository()
    {
        return new UserRepository(CreateDbContext());
    }

    private async Task<User> AddUserAsync()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        await SaveAsync(seed);
        return user;
    }
}
