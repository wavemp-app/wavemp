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
public class OrganizingSettingsServiceTests
{
    private ISettingsStore _store = null!;
    private SettingsSnapshot<OrganizingSettings> _snapshot = null!;
    private ISettingsChangeHandler<OrganizingSettings> _handler = null!;
    private OrganizingSettingsService _service = null!;
    private OrganizingSettings _saved = null!;

    [SetUp]
    public void SetUp()
    {
        _saved = new OrganizingSettings();
        _store = Substitute.For<ISettingsStore>();
        _store.GetAsync<OrganizingSettings>(OrganizingSettings.Key, Arg.Any<CancellationToken>())
            .Returns(_ => _saved);
        _store.SaveAsync(OrganizingSettings.Key, Arg.Any<OrganizingSettings>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _saved = call.ArgAt<OrganizingSettings>(1);
                return Task.CompletedTask;
            });
        _snapshot = new SettingsSnapshot<OrganizingSettings>();
        _handler = Substitute.For<ISettingsChangeHandler<OrganizingSettings>>();
        _service = new OrganizingSettingsService(
            _store,
            _snapshot,
            [_handler],
            NullLogger<OrganizingSettingsService>.Instance);
    }

    [Test]
    public async Task InitializeAsync_WithoutSavedSettings_UsesTheFolderBasedDefault()
    {
        await _service.InitializeAsync();

        _snapshot.Current.LayoutTemplate.Should().Be(OrganizingSettings.DefaultLayoutTemplate);
    }

    [Test]
    public async Task GetAsync_WithAnInvalidSavedTemplate_ReturnsTheDefault()
    {
        _saved = new OrganizingSettings { LayoutTemplate = "{Artist}/{Nope}" };

        OrganizingSettings settings = await _service.GetAsync();

        settings.LayoutTemplate.Should().Be(OrganizingSettings.DefaultLayoutTemplate);
    }

    [Test]
    public async Task UpdateAsync_WithAValidTemplate_SavesItTrimmedAndTellsTheHandler()
    {
        OrganizingSettings previous = _snapshot.Current;

        OrganizingSettingsResult result = await _service.UpdateAsync(
            new UpdateOrganizingSettingsCommand("  {Artist} - {Title} "));

        result.IsSuccess.Should().BeTrue();
        result.Settings!.LayoutTemplate.Should().Be("{Artist} - {Title}");
        _snapshot.Current.Should().BeSameAs(result.Settings);
        await _store.Received(1).SaveAsync(OrganizingSettings.Key, result.Settings, Arg.Any<CancellationToken>());
        await _handler.Received(1).OnSettingsChangedAsync(previous, result.Settings, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateAsync_WithoutATemplate_KeepsTheSavedOne()
    {
        _saved = new OrganizingSettings { LayoutTemplate = "{Artist} - {Title}" };

        OrganizingSettingsResult result = await _service.UpdateAsync(new UpdateOrganizingSettingsCommand());

        result.Settings!.LayoutTemplate.Should().Be("{Artist} - {Title}");
    }

    [Test]
    public async Task UpdateAsync_WithAnInvalidTemplate_SavesNothing()
    {
        OrganizingSettingsResult result = await _service.UpdateAsync(new UpdateOrganizingSettingsCommand("{Artist}"));

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Reason.Should().Be(SettingsErrorReason.Invalid);
        await _store.DidNotReceiveWithAnyArgs().SaveAsync<OrganizingSettings>(default!, default!, default);
        await _handler.DidNotReceiveWithAnyArgs().OnSettingsChangedAsync(default!, default!, default);
    }
}
