using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Caching.Extensions;
using WaveMP.Core.Caching.Jobs;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Caching.Tests.Jobs;

[TestFixture]
public class CoverRuleChangeHandlerTests
{
    private IJobService _jobs = null!;
    private CoverRuleChangeHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _jobs = Substitute.For<IJobService>();
        _jobs.StartRunAsync(Arg.Any<JobRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(JobRunStartResult.Failure(JobRunStartError.AlreadyActive));
        _handler = new CoverRuleChangeHandler(_jobs, NullLogger<CoverRuleChangeHandler>.Instance);
    }

    [TestCase(CoverRuleLevel.Album)]
    [TestCase(CoverRuleLevel.Track)]
    public async Task OnCoverRuleChangedAsync_AnyLevel_StartsThePreparationOfTheCovers(CoverRuleLevel level)
    {
        await _handler.OnCoverRuleChangedAsync(
            new CoverRuleChange(level, Guid.NewGuid(), null, CoverPriority.TrackFirst),
            CancellationToken.None);

        await _jobs.Received(1).StartRunAsync(
            new JobRunRequest(JobIds.CoverCache, JobRunTrigger.CoverRule),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OnSettingsChangedAsync_OtherCoverPriority_StartsThePreparationOfTheCovers()
    {
        await _handler.OnSettingsChangedAsync(
            new LibrarySettings(),
            new LibrarySettings { TrackCoverPriority = CoverPriority.AlbumFirst },
            CancellationToken.None);

        await _jobs.Received(1).StartRunAsync(
            new JobRunRequest(JobIds.CoverCache, JobRunTrigger.CoverRule),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OnSettingsChangedAsync_SameCoverPriority_StartsNothing()
    {
        await _handler.OnSettingsChangedAsync(
            new LibrarySettings(),
            new LibrarySettings { ScanIntervalMinutes = 60 },
            CancellationToken.None);

        await _jobs.DidNotReceiveWithAnyArgs().StartRunAsync(default!, default);
    }

    [Test]
    public void AddWaveMpCaching_Always_RegistersTheHandlerForBothKindsOfChanges()
    {
        ServiceCollection services = new();

        services.AddWaveMpCaching();

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ICoverRuleChangeHandler)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(ISettingsChangeHandler<LibrarySettings>)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
