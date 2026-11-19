using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Core.Settings.Services;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Core.Settings.Stores;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Tests.Services;

[TestFixture]
public class OnlineMetadataSettingsServiceTests
{
    private ISettingsStore _store = null!;
    private SettingsSnapshot<OnlineMetadataSettings> _snapshot = null!;
    private ISettingsChangeHandler<OnlineMetadataSettings> _handler = null!;
    private OnlineMetadataSettingsService _service = null!;
    private OnlineMetadataSettings _saved = null!;

    [SetUp]
    public void SetUp()
    {
        _saved = new OnlineMetadataSettings();
        _store = Substitute.For<ISettingsStore>();
        _store.GetAsync<OnlineMetadataSettings>(OnlineMetadataSettings.Key, Arg.Any<CancellationToken>())
            .Returns(_ => _saved);
        _snapshot = new SettingsSnapshot<OnlineMetadataSettings>();
        _handler = Substitute.For<ISettingsChangeHandler<OnlineMetadataSettings>>();
        _service = new OnlineMetadataSettingsService(
            _store,
            _snapshot,
            [_handler],
            NullLogger<OnlineMetadataSettingsService>.Instance);
    }

    [Test]
    public void Defaults_Always_AreOffWithTheOfficialServicesAndOneRequestPerSecond()
    {
        OnlineMetadataSettings settings = new();

        settings.Enabled.Should().BeFalse();
        settings.MusicBrainzUrl.Should().Be("https://musicbrainz.org");
        settings.CoverArtArchiveUrl.Should().Be("https://coverartarchive.org");
        settings.MusicBrainzRequestIntervalMilliseconds.Should().Be(1000);
    }

    [Test]
    public async Task InitializeAsync_WithSavedSettings_LoadsThemIntoTheSnapshot()
    {
        _saved = new OnlineMetadataSettings { Enabled = true, MusicBrainzUrl = "http://mirror.local" };

        await _service.InitializeAsync();

        _snapshot.Current.Enabled.Should().BeTrue();
        _snapshot.Current.MusicBrainzUrl.Should().Be("http://mirror.local");
    }

    [Test]
    public async Task GetAsync_WithInvalidSavedSettings_ReturnsTheDefaultsThatAreOff()
    {
        _saved = new OnlineMetadataSettings { Enabled = true, MusicBrainzUrl = "nope" };

        OnlineMetadataSettings settings = await _service.GetAsync();

        settings.Enabled.Should().BeFalse();
        settings.MusicBrainzUrl.Should().Be(OnlineMetadataSettings.DefaultMusicBrainzUrl);
    }

    [Test]
    public async Task UpdateAsync_WithValidValues_SavesNormalizedValuesAndTellsTheHandler()
    {
        OnlineMetadataSettings previous = _snapshot.Current;

        OnlineMetadataSettingsResult result = await _service.UpdateAsync(
            new UpdateOnlineMetadataSettingsCommand(true, "http://mirror.local:5000/", null, 0));

        result.IsSuccess.Should().BeTrue();
        result.Settings!.Enabled.Should().BeTrue();
        result.Settings.MusicBrainzUrl.Should().Be("http://mirror.local:5000");
        result.Settings.CoverArtArchiveUrl.Should().Be(OnlineMetadataSettings.DefaultCoverArtArchiveUrl);
        result.Settings.MusicBrainzRequestIntervalMilliseconds.Should().Be(0);
        _snapshot.Current.Should().BeSameAs(result.Settings);
        await _store.Received(1)
            .SaveAsync(OnlineMetadataSettings.Key, result.Settings, Arg.Any<CancellationToken>());
        await _handler.Received(1).OnSettingsChangedAsync(previous, result.Settings, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithInvalidValues_SavesNothing()
    {
        OnlineMetadataSettings previous = _snapshot.Current;

        OnlineMetadataSettingsResult result = await _service.UpdateAsync(
            new UpdateOnlineMetadataSettingsCommand(true, "ftp://mirror", null, -1));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Select(error => error.Field).Should().Equal(
            OnlineMetadataSettingsField.MusicBrainzUrl,
            OnlineMetadataSettingsField.MusicBrainzRequestIntervalMilliseconds);
        _snapshot.Current.Should().BeSameAs(previous);
        await _store.DidNotReceive()
            .SaveAsync(Arg.Any<string>(), Arg.Any<OnlineMetadataSettings>(), Arg.Any<CancellationToken>());
    }
}
