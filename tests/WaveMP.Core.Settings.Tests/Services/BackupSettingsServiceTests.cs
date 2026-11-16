using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Core.Settings.Services;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Core.Settings.Stores;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Tests.Services;

[TestFixture]
public class BackupSettingsServiceTests
{
    private ISettingsStore _store = null!;
    private SettingsSnapshot<BackupSettings> _snapshot = null!;
    private ISettingsChangeHandler<BackupSettings> _firstHandler = null!;
    private ISettingsChangeHandler<BackupSettings> _secondHandler = null!;
    private BackupSettingsService _service = null!;
    private BackupSettings _saved = null!;

    [SetUp]
    public void SetUp()
    {
        _saved = new BackupSettings();
        _store = Substitute.For<ISettingsStore>();
        _store.GetAsync<BackupSettings>(BackupSettings.Key, Arg.Any<CancellationToken>()).Returns(_ => _saved);
        _snapshot = new SettingsSnapshot<BackupSettings>();
        _firstHandler = Substitute.For<ISettingsChangeHandler<BackupSettings>>();
        _secondHandler = Substitute.For<ISettingsChangeHandler<BackupSettings>>();
        _service = new BackupSettingsService(
            _store,
            _snapshot,
            [_firstHandler, _secondHandler],
            NullLogger<BackupSettingsService>.Instance);
    }

    [Test]
    public void Defaults_Always_BackUpDailyAtThreeAndKeepSevenExports()
    {
        BackupSettings settings = new();

        settings.ScheduleTime.Should().Be(new TimeOnly(3, 0));
        settings.RetentionCount.Should().Be(7);
    }

    [Test]
    public async Task InitializeAsync_WithSavedSettings_LoadsThemIntoTheSnapshot()
    {
        _saved = new BackupSettings { ScheduleTime = new TimeOnly(1, 15), RetentionCount = 14 };

        await _service.InitializeAsync();

        _snapshot.Current.ScheduleTime.Should().Be(new TimeOnly(1, 15));
        _snapshot.Current.RetentionCount.Should().Be(14);
    }

    [Test]
    public async Task GetAsync_WithInvalidSavedSettings_ReturnsTheDefaults()
    {
        _saved = new BackupSettings { RetentionCount = 0 };

        BackupSettings settings = await _service.GetAsync();

        settings.RetentionCount.Should().Be(7);
    }

    [Test]
    public async Task UpdateAsync_WithoutValues_KeepsTheSavedOnes()
    {
        _saved = new BackupSettings { ScheduleTime = new TimeOnly(4, 45), RetentionCount = 3 };

        BackupSettingsResult result = await _service.UpdateAsync(new UpdateBackupSettingsCommand());

        result.IsSuccess.Should().BeTrue();
        result.Settings!.ScheduleTime.Should().Be(new TimeOnly(4, 45));
        result.Settings.RetentionCount.Should().Be(3);
    }

    [Test]
    public async Task UpdateAsync_WithValidValues_SavesThemReplacesTheSnapshotAndTellsEveryHandler()
    {
        BackupSettings previous = _snapshot.Current;

        BackupSettingsResult result = await _service.UpdateAsync(
            new UpdateBackupSettingsCommand(new TimeOnly(22, 30), 30));

        result.IsSuccess.Should().BeTrue();
        result.Settings!.ScheduleTime.Should().Be(new TimeOnly(22, 30));
        result.Settings.RetentionCount.Should().Be(30);
        _snapshot.Current.Should().BeSameAs(result.Settings);
        await _store.Received(1).SaveAsync(BackupSettings.Key, result.Settings, Arg.Any<CancellationToken>());
        await _firstHandler.Received(1)
            .OnSettingsChangedAsync(previous, result.Settings, Arg.Any<CancellationToken>());
        await _secondHandler.Received(1)
            .OnSettingsChangedAsync(previous, result.Settings, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithInvalidValues_SavesNothingAndTellsNoHandler()
    {
        BackupSettings previous = _snapshot.Current;

        BackupSettingsResult result = await _service.UpdateAsync(
            new UpdateBackupSettingsCommand(new TimeOnly(3, 0, 30), 101));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Equal(
            new BackupSettingsValidationError(BackupSettingsField.ScheduleTime, SettingsErrorReason.Invalid),
            new BackupSettingsValidationError(
                BackupSettingsField.RetentionCount,
                SettingsErrorReason.OutOfRange,
                1,
                100));
        _snapshot.Current.Should().BeSameAs(previous);
        await _store.DidNotReceive()
            .SaveAsync(Arg.Any<string>(), Arg.Any<BackupSettings>(), Arg.Any<CancellationToken>());
        await _firstHandler.DidNotReceive().OnSettingsChangedAsync(
            Arg.Any<BackupSettings>(),
            Arg.Any<BackupSettings>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WhenAHandlerFails_KeepsTheChangeAndTellsTheOtherHandlers()
    {
        _firstHandler
            .OnSettingsChangedAsync(
                Arg.Any<BackupSettings>(),
                Arg.Any<BackupSettings>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Broken handler"));

        BackupSettingsResult result = await _service.UpdateAsync(new UpdateBackupSettingsCommand(RetentionCount: 5));

        result.IsSuccess.Should().BeTrue();
        _snapshot.Current.RetentionCount.Should().Be(5);
        await _secondHandler.Received(1).OnSettingsChangedAsync(
            Arg.Any<BackupSettings>(),
            result.Settings!,
            Arg.Any<CancellationToken>());
    }
}
