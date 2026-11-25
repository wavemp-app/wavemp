using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Users.Settings;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Users.Tests.Settings;

[TestFixture]
public class UserSettingsServiceTests
{
    private IUserRepository _users = null!;
    private UserSettingsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _users = Substitute.For<IUserRepository>();
        _service = new UserSettingsService(_users);
    }

    [Test]
    public async Task GetAsync_WithUser_ReturnsItsSettings()
    {
        User user = Given("de");

        UserSettings? result = await _service.GetAsync(user.Id);

        result.Should().BeSameAs(user.Settings);
    }

    [Test]
    public async Task GetAsync_WithUnknownUser_ReturnsNull()
    {
        UserSettings? result = await _service.GetAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateAsync_WithLanguage_SavesIt()
    {
        User user = Given("en");

        UserSettings? result = await _service.UpdateAsync(user.Id, new UpdateUserSettingsCommand("de"));

        result!.Language.Should().Be("de");
        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithLanguageWithoutTranslations_SavesItInTheUsualCase()
    {
        User user = Given("en");

        UserSettings? result = await _service.UpdateAsync(user.Id, new UpdateUserSettingsCommand("PT-br"));

        result!.Language.Should().Be("pt-BR");
    }

    [TestCase("")]
    [TestCase("english")]
    [TestCase("de_DE")]
    public async Task UpdateAsync_WithInvalidLanguageTag_ThrowsAndKeepsTheLanguage(string language)
    {
        User user = Given("de");

        Func<Task> act = () => _service.UpdateAsync(user.Id, new UpdateUserSettingsCommand(language));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        user.Settings!.Language.Should().Be("de");
        await _users.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithoutLanguage_KeepsIt()
    {
        User user = Given("de");

        UserSettings? result = await _service.UpdateAsync(user.Id, new UpdateUserSettingsCommand());

        result!.Language.Should().Be("de");
    }

    [Test]
    public async Task UpdateAsync_WithReplayGain_SavesItAndKeepsTheLanguage()
    {
        User user = Given("de");

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(ReplayGain: ReplayGainMode.Album));

        result!.ReplayGain.Should().Be(ReplayGainMode.Album);
        result.Language.Should().Be("de");
        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithoutReplayGain_KeepsIt()
    {
        User user = Given("en");
        user.Settings!.ReplayGain = ReplayGainMode.Track;

        UserSettings? result = await _service.UpdateAsync(user.Id, new UpdateUserSettingsCommand("de"));

        result!.ReplayGain.Should().Be(ReplayGainMode.Track);
    }

    [Test]
    public async Task UpdateAsync_WithPlayerViewMode_SavesItAndKeepsTheOtherSettings()
    {
        User user = Given("de");
        user.Settings!.ReplayGain = ReplayGainMode.Track;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(PlayerViewMode: PlayerViewMode.Visualization));

        result!.PlayerViewMode.Should().Be(PlayerViewMode.Visualization);
        result.Language.Should().Be("de");
        result.ReplayGain.Should().Be(ReplayGainMode.Track);
        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithSidebarCollapsed_SavesItAndKeepsTheOtherSettings()
    {
        User user = Given("de");
        user.Settings!.PlayerViewMode = PlayerViewMode.Cover;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(SidebarCollapsed: true));
        UserSettings? unchanged = await _service.UpdateAsync(user.Id, new UpdateUserSettingsCommand("en"));

        result!.SidebarCollapsed.Should().BeTrue();
        result.PlayerViewMode.Should().Be(PlayerViewMode.Cover);
        unchanged!.SidebarCollapsed.Should().BeTrue();
    }

    [Test]
    public async Task UpdateAsync_WithoutPlayerViewMode_KeepsIt()
    {
        User user = Given("en");
        user.Settings!.PlayerViewMode = PlayerViewMode.Cover;

        UserSettings? result = await _service.UpdateAsync(user.Id, new UpdateUserSettingsCommand("de"));

        result!.PlayerViewMode.Should().Be(PlayerViewMode.Cover);
    }

    [Test]
    public async Task GetAsync_UserWithoutSettings_ReturnsTheDefaults()
    {
        User user = Given("en");
        user.Settings = null;

        UserSettings? result = await _service.GetAsync(user.Id);

        result!.Language.Should().Be("en");
        result.ReplayGain.Should().Be(ReplayGainMode.Off);
        result.PlayerViewMode.Should().Be(PlayerViewMode.Video);
    }

    [Test]
    public async Task GetAsync_UserWithoutSettings_ReturnsThePlaybackDefaults()
    {
        User user = Given("en");
        user.Settings = null;

        UserSettings? result = await _service.GetAsync(user.Id);

        result!.EqualizerEnabled.Should().BeFalse();
        result.EqualizerPreset.Should().Be(EqualizerPreset.Flat);
        result.EqualizerPreampDb.Should().Be(0);
        result.EqualizerCustomGainsDb.Should().Equal(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        result.CrossfadeEnabled.Should().BeFalse();
        result.CrossfadeSeconds.Should().Be(5);
        result.CrossfadeAlbumsGapless.Should().BeTrue();
        result.SleepTimerFadeSeconds.Should().Be(10);
        result.AutoplayEnabled.Should().BeTrue();
        result.AutoplayIncludeVideoOnly.Should().BeTrue();
        result.VideoAmbientMode.Should().BeTrue();
    }

    [Test]
    public async Task UpdateAsync_WithVideoAmbientMode_SavesItAndKeepsTheOtherSettings()
    {
        User user = Given("en");
        user.Settings!.AutoplayEnabled = false;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(VideoAmbientMode: false));

        result!.VideoAmbientMode.Should().BeFalse();
        result.AutoplayEnabled.Should().BeFalse();
        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithoutVideoAmbientMode_KeepsIt()
    {
        User user = Given("en");
        user.Settings!.VideoAmbientMode = false;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(AutoplayEnabled: true));

        result!.VideoAmbientMode.Should().BeFalse();
    }

    [Test]
    public async Task UpdateAsync_WithAutoplay_SavesItAndKeepsTheOtherSettings()
    {
        User user = Given("en");
        user.Settings!.CrossfadeEnabled = true;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(AutoplayEnabled: false, AutoplayIncludeVideoOnly: false));

        result!.AutoplayEnabled.Should().BeFalse();
        result.AutoplayIncludeVideoOnly.Should().BeFalse();
        result.CrossfadeEnabled.Should().BeTrue();
        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithoutAutoplay_KeepsIt()
    {
        User user = Given("en");
        user.Settings!.AutoplayEnabled = false;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(AutoplayIncludeVideoOnly: false));

        result!.AutoplayEnabled.Should().BeFalse();
        result.AutoplayIncludeVideoOnly.Should().BeFalse();
    }

    [Test]
    public async Task UpdateAsync_WithEqualizer_SavesItAndKeepsTheOtherSettings()
    {
        User user = Given("de");
        user.Settings!.ReplayGain = ReplayGainMode.Track;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(
                EqualizerEnabled: true,
                EqualizerPreset: EqualizerPreset.Custom,
                EqualizerPreampDb: -3.5,
                EqualizerCustomGainsDb: [6, 4.5, 3, 1, 0, -1, -2.5, 0, 2, 4]));

        result!.EqualizerEnabled.Should().BeTrue();
        result.EqualizerPreset.Should().Be(EqualizerPreset.Custom);
        result.EqualizerPreampDb.Should().Be(-3.5);
        result.EqualizerCustomGainsDb.Should().Equal(6, 4.5, 3, 1, 0, -1, -2.5, 0, 2, 4);
        result.Language.Should().Be("de");
        result.ReplayGain.Should().Be(ReplayGainMode.Track);
        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithPreset_KeepsTheOwnCurve()
    {
        User user = Given("en");
        user.Settings!.EqualizerCustomGainsDb = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(EqualizerPreset: EqualizerPreset.Rock));

        result!.EqualizerPreset.Should().Be(EqualizerPreset.Rock);
        result.EqualizerCustomGainsDb.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    }

    [Test]
    public async Task UpdateAsync_WithGains_RoundsThemToATenthOfADecibel()
    {
        User user = Given("en");

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(
                EqualizerPreampDb: 1.26,
                EqualizerCustomGainsDb: [0.04, 1.26, 0, 0, 0, 0, 0, 0, 0, -11.96]));

        result!.EqualizerPreampDb.Should().Be(1.3);
        result.EqualizerCustomGainsDb.Should().Equal(0, 1.3, 0, 0, 0, 0, 0, 0, 0, -12);
    }

    [Test]
    public async Task UpdateAsync_WithCrossfadeAndSleepTimer_SavesThem()
    {
        User user = Given("en");
        user.Settings!.EqualizerPreset = EqualizerPreset.Jazz;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(
                CrossfadeEnabled: true,
                CrossfadeSeconds: 12,
                CrossfadeAlbumsGapless: false,
                SleepTimerFadeSeconds: 60));

        result!.CrossfadeEnabled.Should().BeTrue();
        result.CrossfadeSeconds.Should().Be(12);
        result.CrossfadeAlbumsGapless.Should().BeFalse();
        result.SleepTimerFadeSeconds.Should().Be(60);
        result.EqualizerPreset.Should().Be(EqualizerPreset.Jazz);
    }

    [Test]
    public async Task UpdateAsync_WithCrossfadeOff_KeepsItsSeconds()
    {
        User user = Given("en");
        user.Settings!.CrossfadeEnabled = true;
        user.Settings.CrossfadeSeconds = 8;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(CrossfadeEnabled: false));

        result!.CrossfadeEnabled.Should().BeFalse();
        result.CrossfadeSeconds.Should().Be(8);
    }

    [Test]
    public async Task UpdateAsync_WithoutPlaybackSettings_KeepsThem()
    {
        User user = Given("en");
        UserSettings settings = user.Settings!;
        settings.EqualizerEnabled = true;
        settings.EqualizerPreset = EqualizerPreset.Bass;
        settings.EqualizerPreampDb = -2;
        settings.CrossfadeEnabled = true;
        settings.CrossfadeSeconds = 7;
        settings.CrossfadeAlbumsGapless = false;
        settings.SleepTimerFadeSeconds = 20;

        UserSettings? result = await _service.UpdateAsync(user.Id, new UpdateUserSettingsCommand("de"));

        result.Should().BeEquivalentTo(new
        {
            EqualizerEnabled = true,
            EqualizerPreset = EqualizerPreset.Bass,
            EqualizerPreampDb = -2d,
            CrossfadeEnabled = true,
            CrossfadeSeconds = 7,
            CrossfadeAlbumsGapless = false,
            SleepTimerFadeSeconds = 20,
        });
    }

    private static IEnumerable<TestCaseData> InvalidCommands()
    {
        yield return new TestCaseData(new UpdateUserSettingsCommand(EqualizerPreampDb: 12.5)).SetName("Preamp 12.5");
        yield return new TestCaseData(new UpdateUserSettingsCommand(EqualizerPreampDb: double.NaN))
            .SetName("Preamp NaN");
        yield return new TestCaseData(new UpdateUserSettingsCommand(EqualizerCustomGainsDb: new double[9]))
            .SetName("Nine gains");
        yield return new TestCaseData(
                new UpdateUserSettingsCommand(EqualizerCustomGainsDb: [0, 0, 0, 0, 0, 0, 0, 0, 0, -13]))
            .SetName("Gain -13");
        yield return new TestCaseData(new UpdateUserSettingsCommand(EqualizerPreset: (EqualizerPreset)99))
            .SetName("Unknown preset");
        yield return new TestCaseData(new UpdateUserSettingsCommand(CrossfadeSeconds: 13)).SetName("Crossfade 13");
        yield return new TestCaseData(new UpdateUserSettingsCommand(CrossfadeSeconds: -1)).SetName("Crossfade -1");
        yield return new TestCaseData(new UpdateUserSettingsCommand(SleepTimerFadeSeconds: 0)).SetName("Fade 0");
        yield return new TestCaseData(new UpdateUserSettingsCommand(SleepTimerFadeSeconds: 61)).SetName("Fade 61");
        yield return new TestCaseData(new UpdateUserSettingsCommand(VisualizationModule: (VisualizationModule)99))
            .SetName("Unknown visualization module");
        yield return new TestCaseData(new UpdateUserSettingsCommand(VisualizationQuality: (VisualizationQuality)99))
            .SetName("Unknown visualization quality");
        yield return new TestCaseData(new UpdateUserSettingsCommand(VisualizationPreset: new string('a', 201)))
            .SetName("Preset name of 201 characters");
        yield return new TestCaseData(new UpdateUserSettingsCommand(VisualizationAutoChangeSeconds: 4))
            .SetName("Automatic change 4");
        yield return new TestCaseData(new UpdateUserSettingsCommand(VisualizationAutoChangeSeconds: 601))
            .SetName("Automatic change 601");
        yield return new TestCaseData(new UpdateUserSettingsCommand(VisualizationColorScheme: new string('a', 65)))
            .SetName("Color scheme of 65 characters");
        yield return new TestCaseData(
                new UpdateUserSettingsCommand(VisualizationIntensity: (VisualizationIntensity)99))
            .SetName("Unknown visualization intensity");
        yield return new TestCaseData(new UpdateUserSettingsCommand(VisualizationMilkdropSpeed: 9))
            .SetName("Milkdrop speed 9");
        yield return new TestCaseData(new UpdateUserSettingsCommand(VisualizationMilkdropSpeed: 101))
            .SetName("Milkdrop speed 101");
        yield return new TestCaseData(new UpdateUserSettingsCommand(ThemeId: "Clear Glass")).SetName("Theme id");
        yield return new TestCaseData(new UpdateUserSettingsCommand(ThemeId: "")).SetName("Empty theme id");
        yield return new TestCaseData(new UpdateUserSettingsCommand(ThemeAppearance: (ThemeAppearance)99))
            .SetName("Unknown theme appearance");
    }

    [Test]
    public async Task GetAsync_UserWithoutSettings_ReturnsTheThemeDefaults()
    {
        User user = Given("en");
        user.Settings = null;

        UserSettings? result = await _service.GetAsync(user.Id);

        result!.ThemeId.Should().Be("midnight");
        result.ThemeAppearance.Should().Be(ThemeAppearance.System);
    }

    [Test]
    public async Task UpdateAsync_WithTheme_SavesItAndKeepsTheOtherSettings()
    {
        User user = Given("de");

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(ThemeId: "clear-glass", ThemeAppearance: ThemeAppearance.Dark));

        result!.ThemeId.Should().Be("clear-glass");
        result.ThemeAppearance.Should().Be(ThemeAppearance.Dark);
        result.Language.Should().Be("de");
        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithThemeIdOnly_KeepsTheAppearance()
    {
        User user = Given("en");
        user.Settings!.ThemeAppearance = ThemeAppearance.Light;

        UserSettings? result = await _service.UpdateAsync(user.Id, new UpdateUserSettingsCommand(ThemeId: "meadow"));

        result!.ThemeId.Should().Be("meadow");
        result.ThemeAppearance.Should().Be(ThemeAppearance.Light);
    }

    [Test]
    public async Task UpdateAsync_WithoutTheme_KeepsIt()
    {
        User user = Given("en");
        user.Settings!.ThemeId = "accent";
        user.Settings.ThemeAppearance = ThemeAppearance.Dark;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(SidebarCollapsed: true));

        result!.ThemeId.Should().Be("accent");
        result.ThemeAppearance.Should().Be(ThemeAppearance.Dark);
    }

    [Test]
    public async Task UpdateAsync_WithAThemeIdNoClientKnows_SavesIt()
    {
        User user = Given("en");

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(ThemeId: "uploaded-1"));

        result!.ThemeId.Should().Be("uploaded-1");
    }

    [Test]
    public async Task GetAsync_UserWithoutSettings_ReturnsTheVisualizationDefaults()
    {
        User user = Given("en");
        user.Settings = null;

        UserSettings? result = await _service.GetAsync(user.Id);

        result!.VisualizationModule.Should().Be(VisualizationModule.Milkdrop);
        result.VisualizationPreset.Should().BeNull();
        result.VisualizationAutoChange.Should().BeTrue();
        result.VisualizationAutoChangeSeconds.Should().Be(30);
        result.VisualizationQuality.Should().Be(VisualizationQuality.Auto);
        result.VisualizationColorScheme.Should().BeNull();
        result.VisualizationIntensity.Should().Be(VisualizationIntensity.Normal);
        result.VisualizationMilkdropSpeed.Should().Be(50);
    }

    [TestCase(VisualizationModule.AForce, VisualizationModule.BlackCap)]
    [TestCase(VisualizationModule.BlackCap, VisualizationModule.Milkdrop)]
    public async Task UpdateAsync_WithAnotherModule_ForgetsTheColorSchemeOfThePreviousOne(
        VisualizationModule from,
        VisualizationModule to)
    {
        User user = Given("en");
        user.Settings!.VisualizationModule = from;
        user.Settings.VisualizationColorScheme = "ember";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(VisualizationModule: to));

        result!.VisualizationModule.Should().Be(to);
        result.VisualizationColorScheme.Should().BeNull();
    }

    [Test]
    public async Task UpdateAsync_WithAnotherModuleAndItsColorScheme_KeepsTheNamedColorScheme()
    {
        User user = Given("en");
        user.Settings!.VisualizationModule = VisualizationModule.AForce;
        user.Settings.VisualizationColorScheme = "ember";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(
                VisualizationModule: VisualizationModule.BlackCap,
                VisualizationColorScheme: "tide"));

        result!.VisualizationModule.Should().Be(VisualizationModule.BlackCap);
        result.VisualizationColorScheme.Should().Be("tide");
        result.VisualizationPreset.Should().BeNull();
    }

    [Test]
    public async Task UpdateAsync_WithAnotherPresetOfTheSameModule_KeepsTheColorScheme()
    {
        User user = Given("en");
        user.Settings!.VisualizationModule = VisualizationModule.AForce;
        user.Settings.VisualizationPreset = "flow";
        user.Settings.VisualizationColorScheme = "ember";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(
                VisualizationModule: VisualizationModule.AForce,
                VisualizationPreset: "rings"));

        result!.VisualizationPreset.Should().Be("rings");
        result.VisualizationColorScheme.Should().Be("ember");
    }

    [Test]
    public async Task UpdateAsync_WithColorScheme_SavesItAndKeepsThePreset()
    {
        User user = Given("en");
        user.Settings!.VisualizationModule = VisualizationModule.BlackCap;
        user.Settings.VisualizationPreset = "orbit";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(VisualizationColorScheme: new string('a', 64)));

        result!.VisualizationColorScheme.Should().Be(new string('a', 64));
        result.VisualizationPreset.Should().Be("orbit");
    }

    [TestCase("")]
    [TestCase("  ")]
    public async Task UpdateAsync_WithBlankColorScheme_ForgetsTheColorScheme(string colorScheme)
    {
        User user = Given("en");
        user.Settings!.VisualizationColorScheme = "ember";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(VisualizationColorScheme: colorScheme));

        result!.VisualizationColorScheme.Should().BeNull();
    }

    [TestCase(VisualizationIntensity.Reduced, 10)]
    [TestCase(VisualizationIntensity.Normal, 100)]
    public async Task UpdateAsync_WithIntensityAndMilkdropSpeed_SavesThem(
        VisualizationIntensity intensity,
        int speed)
    {
        User user = Given("en");
        user.Settings!.VisualizationIntensity = intensity == VisualizationIntensity.Normal
            ? VisualizationIntensity.Reduced
            : VisualizationIntensity.Normal;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(VisualizationIntensity: intensity, VisualizationMilkdropSpeed: speed));

        result!.VisualizationIntensity.Should().Be(intensity);
        result.VisualizationMilkdropSpeed.Should().Be(speed);
    }

    [TestCase(VisualizationModule.AForce)]
    [TestCase(VisualizationModule.BlackCap)]
    public async Task UpdateAsync_WithAnotherModule_ForgetsThePresetOfThePreviousOne(VisualizationModule module)
    {
        User user = Given("en");
        user.Settings!.VisualizationPreset = "Waves";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(VisualizationModule: module));

        result!.VisualizationModule.Should().Be(module);
        result.VisualizationPreset.Should().BeNull();
    }

    [Test]
    public async Task UpdateAsync_FromAForceToBlackCap_ForgetsTheStyleOfAForce()
    {
        User user = Given("en");
        user.Settings!.VisualizationModule = VisualizationModule.AForce;
        user.Settings.VisualizationPreset = "aurora";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(
                VisualizationModule: VisualizationModule.BlackCap,
                VisualizationPreset: "lumen"));

        result!.VisualizationModule.Should().Be(VisualizationModule.BlackCap);
        result.VisualizationPreset.Should().Be("lumen");
    }

    [Test]
    public async Task UpdateAsync_WithAnotherModuleAndItsPreset_KeepsTheNamedPreset()
    {
        User user = Given("en");
        user.Settings!.VisualizationPreset = "Waves";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(
                VisualizationModule: VisualizationModule.AForce,
                VisualizationPreset: "aurora"));

        result!.VisualizationModule.Should().Be(VisualizationModule.AForce);
        result.VisualizationPreset.Should().Be("aurora");
    }

    [TestCase(VisualizationQuality.Low)]
    [TestCase(VisualizationQuality.Medium)]
    [TestCase(VisualizationQuality.High)]
    [TestCase(VisualizationQuality.Auto)]
    public async Task UpdateAsync_WithQuality_SavesItAndKeepsTheRestOfTheVisualization(VisualizationQuality quality)
    {
        User user = Given("en");
        user.Settings!.VisualizationQuality = VisualizationQuality.Medium;
        user.Settings.VisualizationPreset = "Waves";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(VisualizationQuality: quality));

        result!.VisualizationQuality.Should().Be(quality);
        result.VisualizationPreset.Should().Be("Waves");
        result.VisualizationModule.Should().Be(VisualizationModule.Milkdrop);
    }

    [Test]
    public async Task UpdateAsync_WithVisualization_SavesItAndKeepsTheOtherSettings()
    {
        User user = Given("de");
        user.Settings!.CrossfadeSeconds = 9;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(
                VisualizationModule: VisualizationModule.Milkdrop,
                VisualizationPreset: "Waves",
                VisualizationAutoChange: false,
                VisualizationAutoChangeSeconds: 600));

        result!.VisualizationPreset.Should().Be("Waves");
        result.VisualizationAutoChange.Should().BeFalse();
        result.VisualizationAutoChangeSeconds.Should().Be(600);
        result.Language.Should().Be("de");
        result.CrossfadeSeconds.Should().Be(9);
        await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithoutVisualization_KeepsIt()
    {
        User user = Given("en");
        user.Settings!.VisualizationPreset = "Waves";
        user.Settings.VisualizationAutoChange = false;
        user.Settings.VisualizationAutoChangeSeconds = 45;
        user.Settings.VisualizationQuality = VisualizationQuality.Low;
        user.Settings.VisualizationColorScheme = "ember";
        user.Settings.VisualizationIntensity = VisualizationIntensity.Reduced;
        user.Settings.VisualizationMilkdropSpeed = 75;

        UserSettings? result = await _service.UpdateAsync(user.Id, new UpdateUserSettingsCommand("de"));

        result!.VisualizationColorScheme.Should().Be("ember");
        result.VisualizationIntensity.Should().Be(VisualizationIntensity.Reduced);
        result.VisualizationMilkdropSpeed.Should().Be(75);
        result.VisualizationPreset.Should().Be("Waves");
        result.VisualizationAutoChange.Should().BeFalse();
        result.VisualizationAutoChangeSeconds.Should().Be(45);
        result.VisualizationQuality.Should().Be(VisualizationQuality.Low);
    }

    [Test]
    public async Task UpdateAsync_WithAutoChangeOff_KeepsItsSeconds()
    {
        User user = Given("en");
        user.Settings!.VisualizationAutoChangeSeconds = 120;

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(VisualizationAutoChange: false));

        result!.VisualizationAutoChange.Should().BeFalse();
        result.VisualizationAutoChangeSeconds.Should().Be(120);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task UpdateAsync_WithBlankPreset_ForgetsThePreset(string preset)
    {
        User user = Given("en");
        user.Settings!.VisualizationPreset = "Waves";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(VisualizationPreset: preset));

        result!.VisualizationPreset.Should().BeNull();
    }

    [Test]
    public async Task UpdateAsync_WithTheCurrentModuleOnly_KeepsThePreset()
    {
        User user = Given("en");
        user.Settings!.VisualizationPreset = "Waves";

        UserSettings? result = await _service.UpdateAsync(
            user.Id,
            new UpdateUserSettingsCommand(VisualizationModule: VisualizationModule.Milkdrop));

        result!.VisualizationModule.Should().Be(VisualizationModule.Milkdrop);
        result.VisualizationPreset.Should().Be("Waves");
    }

    [TestCaseSource(nameof(InvalidCommands))]
    public async Task UpdateAsync_WithInvalidPlaybackSetting_ThrowsAndSavesNothing(UpdateUserSettingsCommand command)
    {
        User user = Given("en");

        Func<Task> act = () => _service.UpdateAsync(user.Id, command);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await _users.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithUnknownUser_ReturnsNull()
    {
        UserSettings? result = await _service.UpdateAsync(Guid.NewGuid(), new UpdateUserSettingsCommand("de"));

        result.Should().BeNull();
        await _users.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    private User Given(string language)
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Name = "Anna",
            NormalizedName = "ANNA",
            Initials = "A",
            Color = "#1f6fd6",
            Role = UserRole.User,
            IsBuiltIn = false,
            CreatedAt = DateTimeOffset.UnixEpoch,
            Settings = new UserSettings { Language = language },
        };
        _users.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }
}
