using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Lyrics.Extensions;
using WaveMP.Core.Lyrics.Jobs;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Lyrics.Tests.Jobs;

[TestFixture]
public class LyricsDownloadSettingsHandlerTests
{
    private IJobService _jobs = null!;
    private LyricsDownloadSettingsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _jobs = Substitute.For<IJobService>();
        _jobs.StartRunAsync(Arg.Any<JobRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(JobRunStartResult.Failure(JobRunStartError.AlreadyActive));
        _handler = new LyricsDownloadSettingsHandler(_jobs, NullLogger<LyricsDownloadSettingsHandler>.Instance);
    }

    [Test]
    public async Task OnSettingsChangedAsync_SwitchedOn_StartsTheDownload()
    {
        await _handler.OnSettingsChangedAsync(
            new LibrarySettings(),
            new LibrarySettings { DownloadLyrics = true },
            CancellationToken.None);

        await _jobs.Received(1).StartRunAsync(
            new JobRunRequest(JobIds.LyricsDownload, JobRunTrigger.Settings),
            Arg.Any<CancellationToken>());
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(true, false)]
    public async Task OnSettingsChangedAsync_NotSwitchedOn_StartsNothing(bool before, bool after)
    {
        await _handler.OnSettingsChangedAsync(
            new LibrarySettings { DownloadLyrics = before },
            new LibrarySettings { DownloadLyrics = after },
            CancellationToken.None);

        await _jobs.DidNotReceiveWithAnyArgs().StartRunAsync(default!, default);
    }

    [Test]
    public void AddWaveMpLyrics_RegistersTheHandlerScoped()
    {
        ServiceCollection services = new();

        services.AddWaveMpLyrics();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ISettingsChangeHandler<LibrarySettings>)
            && descriptor.ImplementationType == typeof(LyricsDownloadSettingsHandler)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
