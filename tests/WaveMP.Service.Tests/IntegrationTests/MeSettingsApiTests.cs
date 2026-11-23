using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Databases;

namespace WaveMP.Service.Tests.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class MeSettingsApiTests
{
    private static readonly double[] _zeros = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly double[] _ownCurve = [6, 4.5, 3, 1, 0, -1, -2.5, 0, 2, 4];

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_settings",
            freshDatabase: true);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task GetMySettings_ForNewProfile_ReturnsEnglish()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            UserSettingsDto settings = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            settings.Language.Should().Be("en");
            settings.ReplayGain.Should().Be(ReplayGainModeDto.Off);
            settings.PlayerViewMode.Should().Be(PlayerViewModeDto.Video);
            settings.SidebarCollapsed.Should().BeFalse();
        }
    }

    [Test]
    public async Task UpdateMySettings_WithSidebarCollapsed_KeepsItForTheProfileAndTheOtherSettings()
    {
        (Guid profileId, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await PatchAsync(client, new { language = "de", playerViewMode = "Cover" });

            HttpResponseMessage response = await PatchAsync(client, new { sidebarCollapsed = true });
            using HttpClient otherSession = _factory.CreateAuthorizedClient(
                (await _anonymous.CreateSessionAsync(profileId)).Token);
            UserSettingsDto read = await otherSession.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            await PatchAsync(client, new { sidebarCollapsed = false });
            UserSettingsDto expanded = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("\"sidebarCollapsed\":true");
            read.SidebarCollapsed.Should().BeTrue();
            read.Language.Should().Be("de");
            read.PlayerViewMode.Should().Be(PlayerViewModeDto.Cover);
            expanded.SidebarCollapsed.Should().BeFalse();
        }
    }

    [TestCase("Cover", PlayerViewModeDto.Cover)]
    [TestCase("Visualization", PlayerViewModeDto.Visualization)]
    [TestCase("Video", PlayerViewModeDto.Video)]
    public async Task UpdateMySettings_WithPlayerViewMode_KeepsItAndTheOtherSettings(
        string value,
        PlayerViewModeDto expected)
    {
        (Guid profileId, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await client.PatchAsJsonAsync(
                "/api/v1/me/settings",
                new { language = "de", replayGain = "Album" },
                HttpClientExtensions.JsonOptions);

            HttpResponseMessage response = await client.PatchAsJsonAsync(
                "/api/v1/me/settings",
                new { playerViewMode = value },
                HttpClientExtensions.JsonOptions);
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            using HttpClient otherSession = _factory.CreateAuthorizedClient(
                (await _anonymous.CreateSessionAsync(profileId)).Token);
            UserSettingsDto otherRead = await otherSession.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain($"\"playerViewMode\":\"{value}\"");
            read.PlayerViewMode.Should().Be(expected);
            read.Language.Should().Be("de");
            read.ReplayGain.Should().Be(ReplayGainModeDto.Album);
            otherRead.PlayerViewMode.Should().Be(expected);
        }
    }

    [TestCase("\"Karaoke\"")]
    [TestCase("1")]
    public async Task UpdateMySettings_WithInvalidPlayerViewMode_ReturnsBadRequest(string value)
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            using StringContent body = new($"{{\"playerViewMode\":{value}}}", Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PatchAsync("/api/v1/me/settings", body);

            IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
            errors.Should().ContainSingle(error => error.Field == "playerViewMode")
                .Which.Code.Should().Be(ValidationErrorCodeDto.InvalidFormat);
            (await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings")).PlayerViewMode
                .Should().Be(PlayerViewModeDto.Video);
        }
    }

    [TestCase("Track", ReplayGainModeDto.Track)]
    [TestCase("Album", ReplayGainModeDto.Album)]
    [TestCase("Off", ReplayGainModeDto.Off)]
    public async Task UpdateMySettings_WithReplayGain_KeepsItAndTheLanguage(string value, ReplayGainModeDto expected)
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await client.PatchAsJsonAsync("/api/v1/me/settings", new { language = "de" }, HttpClientExtensions.JsonOptions);

            HttpResponseMessage response = await client.PatchAsJsonAsync(
                "/api/v1/me/settings",
                new { replayGain = value },
                HttpClientExtensions.JsonOptions);
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain($"\"replayGain\":\"{value}\"");
            read.ReplayGain.Should().Be(expected);
            read.Language.Should().Be("de");
        }
    }

    [TestCase("\"Loud\"")]
    [TestCase("1")]
    public async Task UpdateMySettings_WithInvalidReplayGain_ReturnsBadRequest(string value)
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            using StringContent body = new($"{{\"replayGain\":{value}}}", Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PatchAsync("/api/v1/me/settings", body);

            IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
            errors.Should().Contain(error => error.Field == "replayGain");
            (await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings")).ReplayGain
                .Should().Be(ReplayGainModeDto.Off);
        }
    }

    [Test]
    public async Task UpdateMySettings_WithLanguage_KeepsItForTheProfileAcrossSessions()
    {
        (Guid profileId, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            HttpResponseMessage response = await client.PatchAsJsonAsync(
                "/api/v1/me/settings",
                new { language = "de" },
                HttpClientExtensions.JsonOptions);
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            using HttpClient otherSession = _factory.CreateAuthorizedClient(
                (await _anonymous.CreateSessionAsync(profileId)).Token);
            UserSettingsDto otherRead = await otherSession.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("\"language\":\"de\"");
            read.Language.Should().Be("de");
            otherRead.Language.Should().Be("de");
        }
    }

    [Test]
    public async Task UpdateMySettings_WithoutLanguage_KeepsIt()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await client.PatchAsJsonAsync("/api/v1/me/settings", new { language = "de" }, HttpClientExtensions.JsonOptions);

            HttpResponseMessage response = await client.PatchAsJsonAsync(
                "/api/v1/me/settings",
                new { },
                HttpClientExtensions.JsonOptions);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings")).Language.Should().Be("de");
        }
    }

    [TestCase("fr", "fr")]
    [TestCase("PT-br", "pt-BR")]
    [TestCase("zh-hans", "zh-Hans")]
    [TestCase("es-419", "es-419")]
    public async Task UpdateMySettings_WithAnyLanguageTag_SavesItInTheUsualCase(string language, string expected)
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            HttpResponseMessage response = await client.PatchAsJsonAsync(
                "/api/v1/me/settings",
                new { language },
                HttpClientExtensions.JsonOptions);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadFromJsonAsync<UserSettingsDto>(HttpClientExtensions.JsonOptions))!.Language
                .Should().Be(expected);
            (await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings")).Language.Should().Be(expected);
        }
    }

    [TestCase("", ValidationErrorCodeDto.MinLength)]
    [TestCase("english", ValidationErrorCodeDto.InvalidFormat)]
    [TestCase("de_DE", ValidationErrorCodeDto.InvalidFormat)]
    [TestCase("de-", ValidationErrorCodeDto.InvalidFormat)]
    [TestCase("de-DE-x-private", ValidationErrorCodeDto.InvalidFormat)]
    [TestCase("de-abcdefgh-abcdefgh-abcdefgh-abcdefgh", ValidationErrorCodeDto.MaxLength)]
    public async Task UpdateMySettings_WithInvalidLanguageTag_ReturnsTheCodeAndKeepsTheLanguage(
        string language,
        ValidationErrorCodeDto expected)
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            HttpResponseMessage response = await client.PatchAsJsonAsync(
                "/api/v1/me/settings",
                new { language },
                HttpClientExtensions.JsonOptions);

            IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
            errors.Should().ContainSingle(error => error.Field == "language")
                .Which.Code.Should().Be(expected);
            (await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings")).Language.Should().Be("en");
        }
    }

    [TestCase("0")]
    [TestCase("1")]
    [TestCase("7")]
    public async Task UpdateMySettings_WithLanguageAsNumber_ReturnsBadRequestAndKeepsTheLanguage(string language)
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            using StringContent body = new($"{{\"language\":{language}}}", Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PatchAsync("/api/v1/me/settings", body);

            await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "request.invalid");
            (await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings")).Language.Should().Be("en");
        }
    }

    [Test]
    public async Task GetMySettings_ForNewProfile_ReturnsThePlaybackDefaults()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            UserSettingsDto settings = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            settings.Equalizer.Enabled.Should().BeFalse();
            settings.Equalizer.Preset.Should().Be(EqualizerPresetDto.Flat);
            settings.Equalizer.PreampDb.Should().Be(0);
            settings.Equalizer.GainsDb.Should().Equal(_zeros);
            settings.Equalizer.CustomGainsDb.Should().Equal(_zeros);
            settings.Crossfade.Enabled.Should().BeFalse();
            settings.Crossfade.Seconds.Should().Be(5);
            settings.Crossfade.AlbumsGapless.Should().BeTrue();
            settings.SleepTimer.FadeSeconds.Should().Be(10);
            settings.Autoplay.Should().BeEquivalentTo(new { Enabled = true, IncludeVideoOnly = true });
            settings.Video.AmbientMode.Should().BeTrue();
        }
    }

    [Test]
    public async Task UpdateMySettings_WithVideoAmbientMode_KeepsItForTheProfileAndTheOtherSettings()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await PatchAsync(client, new { autoplay = new { enabled = false } });

            HttpResponseMessage response = await PatchAsync(client, new { video = new { ambientMode = false } });
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("\"video\":{\"ambientMode\":false}");
            read.Video.AmbientMode.Should().BeFalse();
            read.Autoplay.Enabled.Should().BeFalse();
        }
    }

    [Test]
    public async Task UpdateMySettings_WithAutoplay_KeepsItForTheProfileAndTheOtherSettings()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await PatchAsync(client, new { crossfade = new { enabled = true }, autoplay = new { enabled = false } });

            HttpResponseMessage response = await PatchAsync(
                client,
                new { autoplay = new { includeVideoOnly = false } });
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain(
                "\"autoplay\":{\"enabled\":false,\"includeVideoOnly\":false}");
            read.Autoplay.Should().BeEquivalentTo(new { Enabled = false, IncludeVideoOnly = false });
            read.Crossfade.Enabled.Should().BeTrue();
        }
    }

    [Test]
    public async Task UpdateMySettings_WithCrossfadeOff_KeepsItsSeconds()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await PatchAsync(client, new { crossfade = new { enabled = true, seconds = 8 } });

            HttpResponseMessage response = await PatchAsync(client, new { crossfade = new { enabled = false } });
            UserSettingsDto off = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            await PatchAsync(client, new { crossfade = new { enabled = true } });
            UserSettingsDto on = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("\"enabled\":false");
            off.Crossfade.Should().BeEquivalentTo(new { Enabled = false, Seconds = 8 });
            on.Crossfade.Should().BeEquivalentTo(new { Enabled = true, Seconds = 8 });
        }
    }

    [Test]
    public async Task UpdateMySettings_WithPreset_AppliesItsCurveAndKeepsTheOwnCurve()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await PatchAsync(client, new { equalizer = new { customGainsDb = _ownCurve } });

            HttpResponseMessage response = await PatchAsync(
                client,
                new { equalizer = new { enabled = true, preset = "Rock" } });
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("\"preset\":\"Rock\"");
            read.Equalizer.Enabled.Should().BeTrue();
            read.Equalizer.Preset.Should().Be(EqualizerPresetDto.Rock);
            read.Equalizer.GainsDb.Should().Equal(5, 4, 3, 1, -1, -1, 1, 3, 4, 5);
            read.Equalizer.CustomGainsDb.Should().Equal(_ownCurve);
        }
    }

    [Test]
    public async Task UpdateMySettings_WithOwnCurve_AppliesItRoundedForTheProfileAcrossSessions()
    {
        (Guid profileId, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            HttpResponseMessage response = await PatchAsync(
                client,
                new
                {
                    equalizer = new
                    {
                        preset = "Custom",
                        preampDb = -2.04,
                        customGainsDb = new[] { 1.26, 0, 0, 0, 0, 0, 0, 0, 0, -11.96 },
                    },
                });
            using HttpClient otherSession = _factory.CreateAuthorizedClient(
                (await _anonymous.CreateSessionAsync(profileId)).Token);
            UserSettingsDto read = await otherSession.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            read.Equalizer.Preset.Should().Be(EqualizerPresetDto.Custom);
            read.Equalizer.PreampDb.Should().Be(-2);
            read.Equalizer.GainsDb.Should().Equal(1.3, 0, 0, 0, 0, 0, 0, 0, 0, -12);
            read.Equalizer.CustomGainsDb.Should().Equal(1.3, 0, 0, 0, 0, 0, 0, 0, 0, -12);
        }
    }

    [Test]
    public async Task UpdateMySettings_WithOneGroup_KeepsTheOtherSettings()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await PatchAsync(
                client,
                new
                {
                    language = "de",
                    replayGain = "Track",
                    playerViewMode = "Cover",
                    equalizer = new { enabled = true, preset = "Jazz", preampDb = -3 },
                    crossfade = new { enabled = true, seconds = 6, albumsGapless = false },
                    sleepTimer = new { fadeSeconds = 30 },
                });

            HttpResponseMessage response = await PatchAsync(client, new { crossfade = new { seconds = 12 } });
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            read.Crossfade.Seconds.Should().Be(12);
            read.Crossfade.Enabled.Should().BeTrue();
            read.Crossfade.AlbumsGapless.Should().BeFalse();
            read.Equalizer.Enabled.Should().BeTrue();
            read.Equalizer.Preset.Should().Be(EqualizerPresetDto.Jazz);
            read.Equalizer.PreampDb.Should().Be(-3);
            read.SleepTimer.FadeSeconds.Should().Be(30);
            read.Language.Should().Be("de");
            read.ReplayGain.Should().Be(ReplayGainModeDto.Track);
            read.PlayerViewMode.Should().Be(PlayerViewModeDto.Cover);
        }
    }

    [Test]
    public async Task GetMySettings_ForNewProfile_ReturnsTheVisualizationDefaults()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            HttpResponseMessage response = await client.GetAsync("/api/v1/me/settings");
            UserSettingsDto settings = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            (await response.Content.ReadAsStringAsync()).Should().Contain(
                "\"visualization\":{\"module\":\"Milkdrop\",\"preset\":null,\"autoChange\":true,\"autoChangeSeconds\":30," +
                "\"quality\":\"Auto\",\"colorScheme\":null,\"intensity\":\"Normal\",\"milkdropSpeed\":50}");
            settings.Visualization.Should().BeEquivalentTo(new
            {
                Module = VisualizationModuleDto.Milkdrop,
                Preset = (string?)null,
                AutoChange = true,
                AutoChangeSeconds = 30,
                Quality = VisualizationQualityDto.Auto,
                ColorScheme = (string?)null,
                Intensity = VisualizationIntensityDto.Normal,
                MilkdropSpeed = 50,
            });
        }
    }

    [Test]
    public async Task UpdateMySettings_WithColorSchemeIntensityAndSpeed_KeepsThemForTheProfile()
    {
        (Guid profileId, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            HttpResponseMessage response = await PatchAsync(
                client,
                new
                {
                    visualization = new
                    {
                        module = "AForce",
                        preset = "rings",
                        colorScheme = "ember",
                        intensity = "Reduced",
                        milkdropSpeed = 25,
                    },
                });
            await PatchAsync(client, new { visualization = new { preset = "warp" } });
            using HttpClient otherSession = _factory.CreateAuthorizedClient(
                (await _anonymous.CreateSessionAsync(profileId)).Token);
            UserSettingsDto read = await otherSession.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            await PatchAsync(client, new { visualization = new { colorScheme = "" } });
            UserSettingsDto forgotten = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should()
                .Contain("\"colorScheme\":\"ember\",\"intensity\":\"Reduced\",\"milkdropSpeed\":25");
            read.Visualization.Should().BeEquivalentTo(new
            {
                Module = VisualizationModuleDto.AForce,
                Preset = "warp",
                ColorScheme = "ember",
                Intensity = VisualizationIntensityDto.Reduced,
                MilkdropSpeed = 25,
            });
            forgotten.Visualization.ColorScheme.Should().BeNull();
            forgotten.Visualization.Preset.Should().Be("warp");
            forgotten.Visualization.Intensity.Should().Be(VisualizationIntensityDto.Reduced);
            forgotten.Visualization.MilkdropSpeed.Should().Be(25);
        }
    }

    [Test]
    public async Task UpdateMySettings_WithAnotherModule_ForgetsTheColorSchemeUnlessNamed()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await PatchAsync(client, new { visualization = new { module = "AForce", colorScheme = "ember" } });
            await PatchAsync(client, new { visualization = new { module = "BlackCap" } });
            UserSettingsDto switched = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            await PatchAsync(client, new { visualization = new { module = "AForce", colorScheme = "prism" } });
            UserSettingsDto named = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            switched.Visualization.Module.Should().Be(VisualizationModuleDto.BlackCap);
            switched.Visualization.ColorScheme.Should().BeNull();
            named.Visualization.Module.Should().Be(VisualizationModuleDto.AForce);
            named.Visualization.ColorScheme.Should().Be("prism");
        }
    }

    [Test]
    public async Task UpdateMySettings_WithBlackCap_KeepsTheModuleAndItsScene()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await PatchAsync(client, new { visualization = new { module = "AForce", preset = "ember" } });

            HttpResponseMessage response = await PatchAsync(
                client,
                new { visualization = new { module = "BlackCap", preset = "lumen" } });
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            await PatchAsync(client, new { visualization = new { module = "Milkdrop" } });
            UserSettingsDto switched = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("\"module\":\"BlackCap\",\"preset\":\"lumen\"");
            read.Visualization.Module.Should().Be(VisualizationModuleDto.BlackCap);
            read.Visualization.Preset.Should().Be("lumen");
            switched.Visualization.Module.Should().Be(VisualizationModuleDto.Milkdrop);
            switched.Visualization.Preset.Should().BeNull();
        }
    }

    [Test]
    public async Task UpdateMySettings_WithVisualization_KeepsItForTheProfileAndTheOtherSettings()
    {
        (Guid profileId, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await PatchAsync(client, new { playerViewMode = "Visualization", crossfade = new { seconds = 7 } });

            HttpResponseMessage response = await PatchAsync(
                client,
                new
                {
                    visualization = new { module = "AForce", preset = "Waves", autoChangeSeconds = 120, quality = "Low" },
                });
            await PatchAsync(client, new { visualization = new { autoChange = false } });
            using HttpClient otherSession = _factory.CreateAuthorizedClient(
                (await _anonymous.CreateSessionAsync(profileId)).Token);
            UserSettingsDto read = await otherSession.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            await PatchAsync(client, new { visualization = new { preset = "" } });
            UserSettingsDto forgotten = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should()
                .Contain("\"module\":\"AForce\",\"preset\":\"Waves\"").And.Contain("\"quality\":\"Low\"");
            read.Visualization.Should().BeEquivalentTo(new
            {
                Module = VisualizationModuleDto.AForce,
                Preset = "Waves",
                AutoChange = false,
                AutoChangeSeconds = 120,
                Quality = VisualizationQualityDto.Low,
            });
            read.PlayerViewMode.Should().Be(PlayerViewModeDto.Visualization);
            read.Crossfade.Seconds.Should().Be(7);
            forgotten.Visualization.Preset.Should().BeNull();
            forgotten.Visualization.AutoChangeSeconds.Should().Be(120);
        }
    }

    private static IEnumerable<TestCaseData> InvalidVisualizationSettings()
    {
        yield return new TestCaseData(
                "{\"visualization\":{\"autoChangeSeconds\":4}}",
                "visualization.autoChangeSeconds",
                ValidationErrorCodeDto.OutOfRange,
                5,
                600)
            .SetArgDisplayNames("AutoChangeTooShort");
        yield return new TestCaseData(
                "{\"visualization\":{\"autoChangeSeconds\":601}}",
                "visualization.autoChangeSeconds",
                ValidationErrorCodeDto.OutOfRange,
                5,
                600)
            .SetArgDisplayNames("AutoChangeTooLong");
        yield return new TestCaseData(
                $"{{\"visualization\":{{\"preset\":\"{new string('a', 201)}\"}}}}",
                "visualization.preset",
                ValidationErrorCodeDto.MaxLength,
                null,
                200)
            .SetArgDisplayNames("PresetTooLong");
        yield return new TestCaseData(
                "{\"visualization\":{\"module\":\"Spectrum\"}}",
                "visualization.module",
                ValidationErrorCodeDto.InvalidFormat,
                null,
                null)
            .SetArgDisplayNames("UnknownModule");
        yield return new TestCaseData(
                "{\"visualization\":{\"quality\":\"Ultra\"}}",
                "visualization.quality",
                ValidationErrorCodeDto.InvalidFormat,
                null,
                null)
            .SetArgDisplayNames("UnknownQuality");
        yield return new TestCaseData(
                $"{{\"visualization\":{{\"colorScheme\":\"{new string('a', 65)}\"}}}}",
                "visualization.colorScheme",
                ValidationErrorCodeDto.MaxLength,
                null,
                64)
            .SetArgDisplayNames("ColorSchemeTooLong");
        yield return new TestCaseData(
                "{\"visualization\":{\"intensity\":\"Extreme\"}}",
                "visualization.intensity",
                ValidationErrorCodeDto.InvalidFormat,
                null,
                null)
            .SetArgDisplayNames("UnknownIntensity");
        yield return new TestCaseData(
                "{\"visualization\":{\"intensity\":1}}",
                "visualization.intensity",
                ValidationErrorCodeDto.InvalidFormat,
                null,
                null)
            .SetArgDisplayNames("IntensityAsNumber");
        yield return new TestCaseData(
                "{\"visualization\":{\"milkdropSpeed\":9}}",
                "visualization.milkdropSpeed",
                ValidationErrorCodeDto.OutOfRange,
                10,
                100)
            .SetArgDisplayNames("MilkdropSpeedTooSlow");
        yield return new TestCaseData(
                "{\"visualization\":{\"milkdropSpeed\":101}}",
                "visualization.milkdropSpeed",
                ValidationErrorCodeDto.OutOfRange,
                10,
                100)
            .SetArgDisplayNames("MilkdropSpeedTooFast");
    }

    [TestCaseSource(nameof(InvalidVisualizationSettings))]
    public async Task UpdateMySettings_WithInvalidVisualizationSetting_ReturnsTheCodeAndKeepsTheSettings(
        string json,
        string field,
        ValidationErrorCodeDto code,
        int? min,
        int? max)
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            using StringContent body = new(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PatchAsync("/api/v1/me/settings", body);

            IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
            errors.Should().ContainSingle(error => error.Field == field)
                .Which.Should().BeEquivalentTo(new { Code = code, Min = min, Max = max });
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            read.Visualization.Preset.Should().BeNull();
            read.Visualization.AutoChangeSeconds.Should().Be(30);
            read.Visualization.ColorScheme.Should().BeNull();
            read.Visualization.Intensity.Should().Be(VisualizationIntensityDto.Normal);
            read.Visualization.MilkdropSpeed.Should().Be(50);
        }
    }

    [Test]
    public async Task GetMySettings_ForNewProfile_ReturnsMidnightWithTheModeOfTheDevice()
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            HttpResponseMessage response = await client.GetAsync("/api/v1/me/settings");

            (await response.Content.ReadAsStringAsync()).Should()
                .Contain("\"theme\":{\"id\":\"midnight\",\"appearance\":\"System\"}");
        }
    }

    [Test]
    public async Task UpdateMySettings_WithTheme_KeepsItForTheProfileAndTheOtherSettings()
    {
        (Guid profileId, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await PatchAsync(client, new { language = "de", sidebarCollapsed = true });

            HttpResponseMessage response = await PatchAsync(
                client,
                new { theme = new { id = "clear-glass", appearance = "Dark" } });
            await PatchAsync(client, new { theme = new { id = "accent" } });
            using HttpClient otherSession = _factory.CreateAuthorizedClient(
                (await _anonymous.CreateSessionAsync(profileId)).Token);
            UserSettingsDto read = await otherSession.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should()
                .Contain("\"theme\":{\"id\":\"clear-glass\",\"appearance\":\"Dark\"}");
            read.Theme.Id.Should().Be("accent");
            read.Theme.Appearance.Should().Be(ThemeAppearanceDto.Dark);
            read.Language.Should().Be("de");
            read.SidebarCollapsed.Should().BeTrue();
        }
    }

    private static IEnumerable<TestCaseData> InvalidThemeSettings()
    {
        yield return new TestCaseData(
                "{\"theme\":{\"id\":\"Clear Glass\"}}",
                "theme.id",
                ValidationErrorCodeDto.InvalidFormat,
                null)
            .SetArgDisplayNames("IdWithSpace");
        yield return new TestCaseData(
                "{\"theme\":{\"id\":\"../midnight\"}}",
                "theme.id",
                ValidationErrorCodeDto.InvalidFormat,
                null)
            .SetArgDisplayNames("IdWithPath");
        yield return new TestCaseData(
                $"{{\"theme\":{{\"id\":\"{new string('a', 65)}\"}}}}",
                "theme.id",
                ValidationErrorCodeDto.MaxLength,
                64)
            .SetArgDisplayNames("IdTooLong");
        yield return new TestCaseData(
                "{\"theme\":{\"appearance\":\"Sepia\"}}",
                "theme.appearance",
                ValidationErrorCodeDto.InvalidFormat,
                null)
            .SetArgDisplayNames("UnknownAppearance");
    }

    [TestCaseSource(nameof(InvalidThemeSettings))]
    public async Task UpdateMySettings_WithInvalidThemeSetting_ReturnsTheCodeAndKeepsTheTheme(
        string json,
        string field,
        ValidationErrorCodeDto code,
        int? max)
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            using StringContent body = new(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PatchAsync("/api/v1/me/settings", body);

            IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
            errors.Should().ContainSingle(error => error.Field == field)
                .Which.Should().BeEquivalentTo(new { Code = code, Max = max });
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            read.Theme.Id.Should().Be("midnight");
            read.Theme.Appearance.Should().Be(ThemeAppearanceDto.System);
        }
    }

    private static IEnumerable<TestCaseData> InvalidPlaybackSettings()
    {
        yield return new TestCaseData(
                "{\"equalizer\":{\"customGainsDb\":[0,0,0,0,0,0,0,0,0]}}",
                "equalizer.customGainsDb",
                ValidationErrorCodeDto.MinItems,
                10,
                null)
            .SetArgDisplayNames("NineGains");
        yield return new TestCaseData(
                "{\"equalizer\":{\"customGainsDb\":[0,0,0,0,0,0,0,0,0,0,0]}}",
                "equalizer.customGainsDb",
                ValidationErrorCodeDto.MaxItems,
                null,
                10)
            .SetArgDisplayNames("ElevenGains");
        yield return new TestCaseData(
                "{\"equalizer\":{\"customGainsDb\":[0,0,0,0,12.5,0,0,0,0,0]}}",
                "equalizer.customGainsDb",
                ValidationErrorCodeDto.OutOfRange,
                -12,
                12)
            .SetArgDisplayNames("GainOutOfRange");
        yield return new TestCaseData(
                "{\"equalizer\":{\"preampDb\":13}}",
                "equalizer.preampDb",
                ValidationErrorCodeDto.OutOfRange,
                -12,
                12)
            .SetArgDisplayNames("Preamp");
        yield return new TestCaseData(
                "{\"crossfade\":{\"seconds\":13}}",
                "crossfade.seconds",
                ValidationErrorCodeDto.OutOfRange,
                0,
                12)
            .SetArgDisplayNames("CrossfadeTooLong");
        yield return new TestCaseData(
                "{\"crossfade\":{\"seconds\":-1}}",
                "crossfade.seconds",
                ValidationErrorCodeDto.OutOfRange,
                0,
                12)
            .SetArgDisplayNames("CrossfadeNegative");
        yield return new TestCaseData(
                "{\"sleepTimer\":{\"fadeSeconds\":0}}",
                "sleepTimer.fadeSeconds",
                ValidationErrorCodeDto.OutOfRange,
                1,
                60)
            .SetArgDisplayNames("Fade");
        yield return new TestCaseData(
                "{\"equalizer\":{\"preset\":\"Loudness\"}}",
                "equalizer.preset",
                ValidationErrorCodeDto.InvalidFormat,
                null,
                null)
            .SetArgDisplayNames("UnknownPreset");
    }

    [TestCaseSource(nameof(InvalidPlaybackSettings))]
    public async Task UpdateMySettings_WithInvalidPlaybackSetting_ReturnsTheCodeAndKeepsTheSettings(
        string json,
        string field,
        ValidationErrorCodeDto code,
        int? min,
        int? max)
    {
        (Guid _, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            using StringContent body = new(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PatchAsync("/api/v1/me/settings", body);

            IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
            errors.Should().ContainSingle(error => error.Field == field)
                .Which.Should().BeEquivalentTo(new { Code = code, Min = min, Max = max });
            UserSettingsDto read = await client.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
            read.Equalizer.Preset.Should().Be(EqualizerPresetDto.Flat);
            read.Equalizer.PreampDb.Should().Be(0);
            read.Equalizer.CustomGainsDb.Should().Equal(_zeros);
            read.Crossfade.Seconds.Should().Be(5);
            read.SleepTimer.FadeSeconds.Should().Be(10);
        }
    }

    [TestCase("GET")]
    [TestCase("PATCH")]
    public async Task MySettings_WithoutToken_ReturnUnauthorized(string method)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), "/api/v1/me/settings")
        {
            Content = method == "PATCH" ? JsonContent.Create(new { language = "de" }) : null,
        };

        HttpResponseMessage response = await _anonymous.SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task UpdateMySettings_ProfileDeletedDuringTheRequest_ReturnsUserNotFound()
    {
        (Guid profileId, HttpClient client) = await CreateProfileAsync();
        using (client)
        {
            await using PendingDeletion deletion =
                await PendingDeletion.OfUserAsync(_factory.ConnectionString, profileId);

            _factory.ErrorLog.Clear();

            // The profile is still visible when the request starts; saving its settings waits for the deletion.
            Task<HttpResponseMessage> request = PatchAsync(client, new { sidebarCollapsed = true });
            bool waited = await deletion.CommitWhileWaitingAsync(request, TimeSpan.FromSeconds(10));
            using HttpResponseMessage response = await request;

            waited.Should().BeTrue();
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "user.not_found");
            _factory.ErrorLog.Errors.Should().BeEmpty();
        }
    }

    private static Task<HttpResponseMessage> PatchAsync(HttpClient client, object request)
    {
        return client.PatchAsJsonAsync("/api/v1/me/settings", request, HttpClientExtensions.JsonOptions);
    }

    private async Task<(Guid ProfileId, HttpClient Client)> CreateProfileAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Settings {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        return (user.Id, _factory.CreateAuthorizedClient(session.Token));
    }
}
