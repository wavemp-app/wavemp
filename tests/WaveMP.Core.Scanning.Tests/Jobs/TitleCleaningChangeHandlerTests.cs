using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Scanning.Extensions;
using WaveMP.Core.Scanning.Jobs;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Tests.Jobs;

[TestFixture]
public class TitleCleaningChangeHandlerTests
{
    private const string FolderBased = OrganizingSettings.DefaultLayoutTemplate;
    private const string FileBased = "{Artist} - {Title}";

    private IJobService _jobs = null!;
    private TitleCleaningChangeHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _jobs = Substitute.For<IJobService>();
        _jobs.StartRunAsync(Arg.Any<JobRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(JobRunStartResult.Failure(JobRunStartError.AlreadyActive));
        _handler = new TitleCleaningChangeHandler(_jobs, NullLogger<TitleCleaningChangeHandler>.Instance);
    }

    [TestCase(FolderBased, FileBased)]
    [TestCase(FileBased, FolderBased)]
    public async Task OnSettingsChangedAsync_SwitchBetweenFolderAndFileBased_StartsAScan(string before, string after)
    {
        await _handler.OnSettingsChangedAsync(Settings(before), Settings(after), CancellationToken.None);

        await _jobs.Received(1).StartRunAsync(
            new JobRunRequest(JobIds.LibraryScan, JobRunTrigger.Settings),
            Arg.Any<CancellationToken>());
    }

    [TestCase(FolderBased, "{Artist}/{Title}")]
    [TestCase(FileBased, "{TrackNumber} {Artist} - {Title}")]
    public async Task OnSettingsChangedAsync_SameKindOfTemplate_StartsNothing(string before, string after)
    {
        await _handler.OnSettingsChangedAsync(Settings(before), Settings(after), CancellationToken.None);

        await _jobs.DidNotReceiveWithAnyArgs().StartRunAsync(default!, default);
    }

    [Test]
    public void TitleCleaningModes_FollowTheKindOfTemplate()
    {
        TitleCleaningModes.Of(Settings(FolderBased)).Should().Be(TitleCleaningMode.ExactArtistTag);
        TitleCleaningModes.Of(Settings(FileBased)).Should().Be(TitleCleaningMode.LooseArtist);
    }

    [Test]
    public void AddWaveMpScanning_RegistersTheHandlerScoped()
    {
        ServiceCollection services = new();

        services.AddWaveMpScanning();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ISettingsChangeHandler<OrganizingSettings>)
            && descriptor.ImplementationType == typeof(TitleCleaningChangeHandler)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static OrganizingSettings Settings(string template)
    {
        return new OrganizingSettings { LayoutTemplate = template };
    }
}
