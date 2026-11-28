using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using WaveMP.Core.Analysis.Jobs;
using WaveMP.Core.Analysis.Options;
using WaveMP.Core.Analysis.Services;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Core.Transcoding.Accelerators;
using WaveMP.Core.Transcoding.Services;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Analysis.Tests.Jobs;

[TestFixture]
public class SongAnalysisJobTests
{
    [Test]
    public void SongAnalysisJob_Always_DisallowsConcurrentExecution()
    {
        typeof(SongAnalysisJob).GetCustomAttribute<DisallowConcurrentExecutionAttribute>().Should().NotBeNull();
    }

    [TestCase(true, 1)]
    [TestCase(false, 0)]
    public async Task Execute_AfterTheAnalysis_StartsTheLyricsDownloadIfSwitchedOn(bool downloadLyrics, int expected)
    {
        ISongAnalysisRunner runner = Substitute.For<ISongAnalysisRunner>();
        SongAnalysisJobResult result = new(3, 1, 0);
        runner.RunAsync(Arg.Any<CancellationToken>()).Returns(result);
        IJobService jobs = Substitute.For<IJobService>();
        IJobExecutionContext context = Substitute.For<IJobExecutionContext>();

        await CreateJob(runner, jobs, downloadLyrics).Execute(context, CancellationToken.None);

        context.Received().Result = result;
        Received.InOrder(() =>
        {
            runner.RunAsync(Arg.Any<CancellationToken>());
            if (expected > 0)
            {
                jobs.StartRunAsync(
                    new JobRunRequest(JobIds.LyricsDownload, JobRunTrigger.LibraryScan),
                    Arg.Any<CancellationToken>());
            }
        });
        await jobs.Received(expected).StartRunAsync(Arg.Any<JobRunRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Execute_DownloadCannotBeStarted_KeepsTheRunSuccessful()
    {
        ISongAnalysisRunner runner = Substitute.For<ISongAnalysisRunner>();
        runner.RunAsync(Arg.Any<CancellationToken>()).Returns(new SongAnalysisJobResult(0, 0, 0));
        IJobService jobs = Substitute.For<IJobService>();
        jobs.StartRunAsync(Arg.Any<JobRunRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database gone"));

        Func<Task> execute = async () => await CreateJob(runner, jobs, true)
            .Execute(Substitute.For<IJobExecutionContext>(), CancellationToken.None);

        await execute.Should().NotThrowAsync();
    }

    [Test]
    public async Task RunAsync_TracksInBatches_AnalyzesEachAndCountsTheResults()
    {
        ISongAnalysisRepository repository = Substitute.For<ISongAnalysisRepository>();
        Guid[] tracks = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        repository.ListTracksToAnalyzeAsync(
                AnalysisOptions.Version,
                SongAnalysisRunner.BatchSize,
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns([tracks[0], tracks[1]], [tracks[2]], []);
        ISongAnalyzer analyzer = Substitute.For<ISongAnalyzer>();
        analyzer.AnalyzeAsync(tracks[0], Arg.Any<CancellationToken>())
            .Returns(new SongAnalysisResult(SongAnalysisOutcome.Analyzed, NeedsReview: true));
        analyzer.AnalyzeAsync(tracks[1], Arg.Any<CancellationToken>())
            .Returns(new SongAnalysisResult(SongAnalysisOutcome.Analyzed, Failed: true));
        analyzer.AnalyzeAsync(tracks[2], Arg.Any<CancellationToken>())
            .Returns(new SongAnalysisResult(SongAnalysisOutcome.Analyzed));

        SongAnalysisJobResult result = await CreateRunner(repository, analyzer, IdleTranscoding()).RunAsync();

        result.Should().Be(new SongAnalysisJobResult(3, 1, 1));
        await analyzer.Received(3).AnalyzeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_TrackThatKeepsFailing_IsLeftForTheNextRun()
    {
        ISongAnalysisRepository repository = Substitute.For<ISongAnalysisRepository>();
        var track = Guid.NewGuid();
        repository.ListTracksToAnalyzeAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => ((IReadOnlyCollection<Guid>)call[2]).Contains(track) ? [] : [track]);
        ISongAnalyzer analyzer = Substitute.For<ISongAnalyzer>();
        analyzer.AnalyzeAsync(track, Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("Disk gone"));

        SongAnalysisJobResult result = await CreateRunner(repository, analyzer, IdleTranscoding()).RunAsync();

        result.Failed.Should().Be(1);
        await analyzer.Received(1).AnalyzeAsync(track, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_WhileTranscodesRun_WaitsBeforeTheNextTrack()
    {
        ISongAnalysisRepository repository = Substitute.For<ISongAnalysisRepository>();
        var track = Guid.NewGuid();
        repository.ListTracksToAnalyzeAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns([track], Array.Empty<Guid>());
        ISongAnalyzer analyzer = Substitute.For<ISongAnalyzer>();
        analyzer.AnalyzeAsync(track, Arg.Any<CancellationToken>())
            .Returns(new SongAnalysisResult(SongAnalysisOutcome.Analyzed));
        ITranscodingService transcoding = Substitute.For<ITranscodingService>();
        int checks = 0;
        bool analyzedWhileTranscoding = false;
        transcoding.GetStatus().Returns(_ => Status(++checks <= 2 ? 1 : 0));
        analyzer.When(service => service.AnalyzeAsync(track, Arg.Any<CancellationToken>()))
            .Do(_ => analyzedWhileTranscoding = checks < 3);

        SongAnalysisJobResult result = await CreateRunner(repository, analyzer, transcoding, maxWait: 1000)
            .RunAsync()
            .WaitAsync(TimeSpan.FromSeconds(10));

        result.TracksAnalyzed.Should().Be(1);
        checks.Should().Be(3);
        analyzedWhileTranscoding.Should().BeFalse();
    }

    [Test]
    public async Task RunAsync_TranscodesForTooLong_GoesOnAtItsLowPriority()
    {
        ISongAnalysisRepository repository = Substitute.For<ISongAnalysisRepository>();
        var track = Guid.NewGuid();
        repository.ListTracksToAnalyzeAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns([track], Array.Empty<Guid>());
        ISongAnalyzer analyzer = Substitute.For<ISongAnalyzer>();
        analyzer.AnalyzeAsync(track, Arg.Any<CancellationToken>())
            .Returns(new SongAnalysisResult(SongAnalysisOutcome.Analyzed));
        ITranscodingService transcoding = Substitute.For<ITranscodingService>();
        transcoding.GetStatus().Returns(Status(2));

        SongAnalysisJobResult result = await CreateRunner(repository, analyzer, transcoding, maxWait: 50)
            .RunAsync()
            .WaitAsync(TimeSpan.FromSeconds(10));

        result.TracksAnalyzed.Should().Be(1);
    }

    private static SongAnalysisJob CreateJob(ISongAnalysisRunner runner, IJobService jobs, bool downloadLyrics)
    {
        ISettingsSnapshot<LibrarySettings> settings = Substitute.For<ISettingsSnapshot<LibrarySettings>>();
        settings.Current.Returns(new LibrarySettings { DownloadLyrics = downloadLyrics });
        return new SongAnalysisJob(runner, jobs, settings, NullLogger<SongAnalysisJob>.Instance);
    }

    private static SongAnalysisRunner CreateRunner(
        ISongAnalysisRepository repository,
        ISongAnalyzer analyzer,
        ITranscodingService transcoding,
        int maxWait = 0)
    {
        return new SongAnalysisRunner(
            repository,
            analyzer,
            transcoding,
            Microsoft.Extensions.Options.Options.Create(new AnalysisOptions
            {
                TranscodeWaitInterval = TimeSpan.FromMilliseconds(10),
                MaxTranscodeWait = TimeSpan.FromMilliseconds(maxWait),
            }),
            TimeProvider.System,
            NullLogger<SongAnalysisRunner>.Instance);
    }

    private static ITranscodingService IdleTranscoding()
    {
        ITranscodingService transcoding = Substitute.For<ITranscodingService>();
        transcoding.GetStatus().Returns(Status(0));
        return transcoding;
    }

    private static TranscodingStatus Status(int runningJobs)
    {
        return new TranscodingStatus(HardwareAccelerationStatus.Detecting, true, runningJobs, 2);
    }
}
