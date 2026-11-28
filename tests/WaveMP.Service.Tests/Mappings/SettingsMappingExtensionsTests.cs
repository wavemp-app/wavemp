using AwesomeAssertions;
using WaveMP.Core.Users.Settings;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class SettingsMappingExtensionsTests
{
    [Test]
    public void ReplayGainModes_MapBothWays()
    {
        foreach (ReplayGainMode mode in Enum.GetValues<ReplayGainMode>())
        {
            ReplayGainModeDto dto = mode.ToDto();

            dto.ToString().Should().Be(mode.ToString());
            dto.ToEntity().Should().Be(mode);
        }

        Enum.GetValues<ReplayGainModeDto>().Should().HaveSameCount(Enum.GetValues<ReplayGainMode>());
    }

    [Test]
    public void PlayerViewModes_MapBothWays()
    {
        foreach (PlayerViewMode mode in Enum.GetValues<PlayerViewMode>())
        {
            PlayerViewModeDto dto = mode.ToDto();

            dto.ToString().Should().Be(mode.ToString());
            dto.ToEntity().Should().Be(mode);
        }

        Enum.GetValues<PlayerViewModeDto>().Should().HaveSameCount(Enum.GetValues<PlayerViewMode>());
    }

    [Test]
    public void ToDto_Settings_CarriesAllSettings()
    {
        UserSettings settings = new()
        {
            Language = "de",
            ReplayGain = ReplayGainMode.Album,
            PlayerViewMode = PlayerViewMode.Visualization,
        };

        UserSettingsDto dto = settings.ToDto();

        dto.Language.Should().Be("de");
        dto.ReplayGain.Should().Be(ReplayGainModeDto.Album);
        dto.PlayerViewMode.Should().Be(PlayerViewModeDto.Visualization);
    }

    [Test]
    public void EqualizerPresets_MapBothWays()
    {
        foreach (EqualizerPreset preset in Enum.GetValues<EqualizerPreset>())
        {
            EqualizerPresetDto dto = preset.ToDto();

            dto.ToString().Should().Be(preset.ToString());
            dto.ToEntity().Should().Be(preset);
        }

        Enum.GetValues<EqualizerPresetDto>().Should().HaveSameCount(Enum.GetValues<EqualizerPreset>());
    }

    [Test]
    public void ToDto_Settings_CarriesThePlaybackGroups()
    {
        UserSettings settings = new()
        {
            EqualizerEnabled = true,
            EqualizerPreset = EqualizerPreset.Bass,
            EqualizerPreampDb = -4.5,
            EqualizerCustomGainsDb = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
            CrossfadeSeconds = 8,
            CrossfadeAlbumsGapless = false,
            SleepTimerFadeSeconds = 45,
            AutoplayEnabled = false,
            AutoplayIncludeVideoOnly = true,
            VideoAmbientMode = false,
            VideoSkipIntroAndOutro = false,
        };

        UserSettingsDto dto = settings.ToDto();

        dto.Equalizer.Should().BeEquivalentTo(new
        {
            Enabled = true,
            Preset = EqualizerPresetDto.Bass,
            PreampDb = -4.5,
            GainsDb = new double[] { 8, 7, 5, 3, 1, 0, 0, 0, 0, 0 },
            CustomGainsDb = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
        });
        dto.Crossfade.Should().BeEquivalentTo(new { Seconds = 8, AlbumsGapless = false });
        dto.SleepTimer.FadeSeconds.Should().Be(45);
        dto.Autoplay.Should().BeEquivalentTo(new { Enabled = false, IncludeVideoOnly = true });
        dto.Video.Should().BeEquivalentTo(new { AmbientMode = false, SkipIntroAndOutro = false });
    }

    [Test]
    public void ToDto_CustomPreset_AppliesTheOwnCurve()
    {
        UserSettings settings = new()
        {
            EqualizerPreset = EqualizerPreset.Custom,
            EqualizerCustomGainsDb = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
        };

        settings.ToDto().Equalizer.GainsDb.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    }

    [Test]
    public void ToCommand_Groups_FillTheCommand()
    {
        UpdateUserSettingsDto request = new()
        {
            Equalizer = new UpdateEqualizerSettingsDto
            {
                Enabled = true,
                Preset = EqualizerPresetDto.Custom,
                PreampDb = 2,
                CustomGainsDb = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
            },
            Crossfade = new UpdateCrossfadeSettingsDto { Seconds = 4, AlbumsGapless = true },
            SleepTimer = new UpdateSleepTimerSettingsDto { FadeSeconds = 20 },
            Autoplay = new UpdateAutoplaySettingsDto { IncludeVideoOnly = false },
            Video = new UpdateVideoSettingsDto { AmbientMode = false, SkipIntroAndOutro = true },
        };

        UpdateUserSettingsCommand command = request.ToCommand();

        command.Should().BeEquivalentTo(new
        {
            Language = (string?)null,
            EqualizerEnabled = true,
            EqualizerPreset = EqualizerPreset.Custom,
            EqualizerPreampDb = 2.0,
            EqualizerCustomGainsDb = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            CrossfadeSeconds = 4,
            CrossfadeAlbumsGapless = true,
            SleepTimerFadeSeconds = 20,
            AutoplayEnabled = (bool?)null,
            AutoplayIncludeVideoOnly = false,
            VideoAmbientMode = false,
            VideoSkipIntroAndOutro = true,
        });
    }

    [Test]
    public void VisualizationModules_MapBothWays()
    {
        foreach (VisualizationModule module in Enum.GetValues<VisualizationModule>())
        {
            VisualizationModuleDto dto = module.ToDto();

            dto.ToString().Should().Be(module.ToString());
            dto.ToEntity().Should().Be(module);
        }

        Enum.GetValues<VisualizationModuleDto>().Should().HaveSameCount(Enum.GetValues<VisualizationModule>());
    }

    [Test]
    public void VisualizationQualities_MapBothWays()
    {
        foreach (VisualizationQuality quality in Enum.GetValues<VisualizationQuality>())
        {
            VisualizationQualityDto dto = quality.ToDto();

            dto.ToString().Should().Be(quality.ToString());
            dto.ToEntity().Should().Be(quality);
        }

        Enum.GetValues<VisualizationQualityDto>().Should().HaveSameCount(Enum.GetValues<VisualizationQuality>());
    }

    [Test]
    public void VisualizationIntensities_MapBothWays()
    {
        foreach (VisualizationIntensity intensity in Enum.GetValues<VisualizationIntensity>())
        {
            VisualizationIntensityDto dto = intensity.ToDto();

            dto.ToString().Should().Be(intensity.ToString());
            dto.ToEntity().Should().Be(intensity);
        }

        Enum.GetValues<VisualizationIntensityDto>().Should()
            .HaveSameCount(Enum.GetValues<VisualizationIntensity>());
    }

    [Test]
    public void ToDto_Settings_CarriesTheVisualization()
    {
        UserSettings settings = new()
        {
            VisualizationModule = VisualizationModule.AForce,
            VisualizationPreset = "aurora",
            VisualizationAutoChange = false,
            VisualizationAutoChangeSeconds = 120,
            VisualizationQuality = VisualizationQuality.Medium,
            VisualizationColorScheme = "ember",
            VisualizationIntensity = VisualizationIntensity.Reduced,
            VisualizationMilkdropSpeed = 75,
        };

        UserSettingsDto dto = settings.ToDto();

        dto.Visualization.Should().BeEquivalentTo(new
        {
            Module = VisualizationModuleDto.AForce,
            Preset = "aurora",
            AutoChange = false,
            AutoChangeSeconds = 120,
            Quality = VisualizationQualityDto.Medium,
            ColorScheme = "ember",
            Intensity = VisualizationIntensityDto.Reduced,
            MilkdropSpeed = 75,
        });
    }

    [Test]
    public void ToCommand_Visualization_FillsTheCommand()
    {
        UpdateUserSettingsDto request = new()
        {
            Visualization = new UpdateVisualizationSettingsDto
            {
                Module = VisualizationModuleDto.AForce,
                Preset = "Waves",
                AutoChange = true,
                AutoChangeSeconds = 15,
                Quality = VisualizationQualityDto.Low,
                ColorScheme = "prism",
                Intensity = VisualizationIntensityDto.Reduced,
                MilkdropSpeed = 25,
            },
        };

        UpdateUserSettingsCommand command = request.ToCommand();

        command.Should().Be(new UpdateUserSettingsCommand(
            VisualizationModule: VisualizationModule.AForce,
            VisualizationPreset: "Waves",
            VisualizationAutoChange: true,
            VisualizationAutoChangeSeconds: 15,
            VisualizationQuality: VisualizationQuality.Low,
            VisualizationColorScheme: "prism",
            VisualizationIntensity: VisualizationIntensity.Reduced,
            VisualizationMilkdropSpeed: 25));
    }

    [Test]
    public void ToCommand_MissingGroups_KeepEverything()
    {
        UpdateUserSettingsCommand command = new UpdateUserSettingsDto().ToCommand();

        command.Should().Be(new UpdateUserSettingsCommand());
    }
}
