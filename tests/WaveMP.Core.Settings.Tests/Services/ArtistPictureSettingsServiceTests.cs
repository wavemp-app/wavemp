using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Core.Settings.Services;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Core.Settings.Stores;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Tests.Services;

[TestFixture]
public class ArtistPictureSettingsServiceTests
{
    private const string Key = "0123456789abcdefABCDEF";

    private ISettingsStore _store = null!;
    private SettingsSnapshot<ArtistPictureSettings> _snapshot = null!;
    private ISettingsChangeHandler<ArtistPictureSettings> _handler = null!;
    private CapturingLogger _logger = null!;
    private ArtistPictureSettingsService _service = null!;
    private ArtistPictureSettings _saved = null!;

    [SetUp]
    public void SetUp()
    {
        _saved = new ArtistPictureSettings();
        _store = Substitute.For<ISettingsStore>();
        _store.GetAsync<ArtistPictureSettings>(ArtistPictureSettings.Key, Arg.Any<CancellationToken>())
            .Returns(_ => _saved);
        _store.SaveAsync(ArtistPictureSettings.Key, Arg.Any<ArtistPictureSettings>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _saved = call.ArgAt<ArtistPictureSettings>(1);
                return Task.CompletedTask;
            });
        _snapshot = new SettingsSnapshot<ArtistPictureSettings>();
        _handler = Substitute.For<ISettingsChangeHandler<ArtistPictureSettings>>();
        _logger = new CapturingLogger();
        _service = new ArtistPictureSettingsService(_store, _snapshot, [_handler], _logger);
    }

    [Test]
    public async Task InitializeAsync_WithoutSavedSettings_KeepsTheLookupOff()
    {
        await _service.InitializeAsync();

        _snapshot.Current.OnlineLookupEnabled.Should().BeFalse();
        _snapshot.Current.FanartTvApiKey.Should().BeNull();
    }

    [Test]
    public async Task GetAsync_WithAnInvalidSavedKey_ReturnsTheDefaultsThatAreOff()
    {
        _saved = new ArtistPictureSettings { OnlineLookupEnabled = true, FanartTvApiKey = "not a key" };

        ArtistPictureSettings settings = await _service.GetAsync();

        settings.OnlineLookupEnabled.Should().BeFalse();
        settings.FanartTvApiKey.Should().BeNull();
    }

    [Test]
    public async Task UpdateAsync_WithValidValues_SavesThemAndTellsTheHandler()
    {
        ArtistPictureSettings previous = _snapshot.Current;

        ArtistPictureSettingsResult result = await _service.UpdateAsync(
            new UpdateArtistPictureSettingsCommand(true, $"  {Key} "));

        result.IsSuccess.Should().BeTrue();
        result.Settings!.OnlineLookupEnabled.Should().BeTrue();
        result.Settings.FanartTvApiKey.Should().Be(Key);
        _snapshot.Current.Should().BeSameAs(result.Settings);
        await _store.Received(1)
            .SaveAsync(ArtistPictureSettings.Key, result.Settings, Arg.Any<CancellationToken>());
        await _handler.Received(1).OnSettingsChangedAsync(previous, result.Settings, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithoutApiKey_KeepsTheStoredKey()
    {
        _saved = new ArtistPictureSettings { FanartTvApiKey = Key };

        ArtistPictureSettingsResult result = await _service.UpdateAsync(
            new UpdateArtistPictureSettingsCommand(OnlineLookupEnabled: true));

        result.Settings!.FanartTvApiKey.Should().Be(Key);
        result.Settings.OnlineLookupEnabled.Should().BeTrue();
    }

    [Test]
    public async Task UpdateAsync_WithAnEmptyApiKey_RemovesTheStoredKey()
    {
        _saved = new ArtistPictureSettings { OnlineLookupEnabled = true, FanartTvApiKey = Key };

        ArtistPictureSettingsResult result = await _service.UpdateAsync(
            new UpdateArtistPictureSettingsCommand(FanartTvApiKey: string.Empty));

        result.Settings!.FanartTvApiKey.Should().BeNull();
        result.Settings.OnlineLookupEnabled.Should().BeTrue();
    }

    [Test]
    public async Task UpdateAsync_WithAnInvalidApiKey_SavesNothing()
    {
        ArtistPictureSettings previous = _snapshot.Current;

        ArtistPictureSettingsResult result = await _service.UpdateAsync(
            new UpdateArtistPictureSettingsCommand(true, "key\nwith a line break"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Equal(new ArtistPictureSettingsValidationError(
            ArtistPictureSettingsField.FanartTvApiKey,
            SettingsErrorReason.Invalid));
        _snapshot.Current.Should().BeSameAs(previous);
        await _store.DidNotReceive()
            .SaveAsync(Arg.Any<string>(), Arg.Any<ArtistPictureSettings>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_SetAndRemoveTheApiKey_LogsOnlyThatItChangedNeverTheKey()
    {
        await _service.UpdateAsync(new UpdateArtistPictureSettingsCommand(true, Key));
        await _service.UpdateAsync(new UpdateArtistPictureSettingsCommand(FanartTvApiKey: string.Empty));

        _logger.Entries.Should().Contain(entry => entry.Contains("fanart.tv set"))
            .And.Contain(entry => entry.Contains("fanart.tv removed"))
            .And.NotContain(entry => entry.Contains(Key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Keeps every log line with the values of its placeholders.
    /// </summary>
    private sealed class CapturingLogger : ILogger<ArtistPictureSettingsService>
    {
        private readonly ConcurrentQueue<string> _entries = new();

        public IReadOnlyCollection<string> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(" ", pairs.Select(pair => $"{pair.Key}={pair.Value}"))
                : string.Empty;
            _entries.Enqueue($"{formatter(state, exception)} {values} {exception}");
        }
    }
}
