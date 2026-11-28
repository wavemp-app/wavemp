using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Jobs.Schedulers;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Scanning.Jobs;
using WaveMP.Core.Scanning.Options;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Tests.Jobs;

[TestFixture]
public class LibraryScanJobTests
{
    private static readonly Guid _source = Guid.NewGuid();
    private static readonly Guid _otherSource = Guid.NewGuid();

    [Test]
    public void LibraryScanJob_Always_DisallowsConcurrentExecution()
    {
        typeof(LibraryScanJob).GetCustomAttribute<DisallowConcurrentExecutionAttribute>().Should().NotBeNull();
    }

    [Test]
    public async Task Execute_WithPayload_ScansTheFoldersForTheRunAndSetsTheResult()
    {
        ILibraryScanner scanner = Substitute.For<ILibraryScanner>();
        JobRunTracker tracker = new();
        var runId = Guid.NewGuid();
        tracker.MarkRunning(JobIds.LibraryScan, runId, "fire-1");
        ScanResult result = new(new ScanStatistics { TracksTotal = 3 }, []);
        scanner.ScanAsync(Arg.Any<ScanScope>(), runId, Arg.Any<CancellationToken>()).Returns(result);
        IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
        context.FireInstanceId.Returns("fire-1");
        context.MergedJobDataMap.Returns(new JobDataMap
        {
            [JobDataKeys.Payload] = LibraryScanJob.CreatePayload(ScanScope.Of(
            [
                new SourceFolder(_source, "Artist/Album"),
                new SourceFolder(_source, "Other"),
                new SourceFolder(_otherSource, string.Empty),
            ])),
        });

        await CreateJob(scanner, Substitute.For<IJobService>(), tracker).Execute(context, CancellationToken.None);

        await scanner.Received(1).ScanAsync(
            Arg.Is<ScanScope>(scope => scope.DirectoriesOf(_source)!.SequenceEqual(new[] { "Artist/Album", "Other" })
                && scope.DirectoriesOf(_otherSource)!.SequenceEqual(new[] { string.Empty })),
            runId,
            Arg.Any<CancellationToken>());
        context.Received().Result = result;
    }

    [TestCase(true, 0, true)]
    [TestCase(false, 1, true)]
    [TestCase(false, 0, false)]
    public async Task Execute_AfterTheScan_StartsThePreparationOfTheCoversAfterFullScansAndChanges(
        bool full,
        int filesAdded,
        bool expected)
    {
        ILibraryScanner scanner = Substitute.For<ILibraryScanner>();
        scanner.ScanAsync(Arg.Any<ScanScope>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ScanResult(new ScanStatistics { FilesAdded = filesAdded, IsPartial = !full }, []));
        IJobService jobs = Substitute.For<IJobService>();
        IJobExecutionContext context = ContextFor(full ? null : ScanScope.Of(_source, ["Artist/Album"]));

        await CreateJob(scanner, jobs).Execute(context, CancellationToken.None);

        await jobs.Received(expected ? 1 : 0).StartRunAsync(
            new JobRunRequest(JobIds.CoverCache, JobRunTrigger.LibraryScan),
            Arg.Any<CancellationToken>());
    }

    [TestCase(true, 0, 1)]
    [TestCase(false, 1, 1)]
    [TestCase(false, 0, 0)]
    public async Task Execute_AfterTheScan_StartsTheSongAnalysisAfterFullScansAndChangesButNotTheLyricsDownload(
        bool full,
        int filesAdded,
        int expected)
    {
        ILibraryScanner scanner = Substitute.For<ILibraryScanner>();
        scanner.ScanAsync(Arg.Any<ScanScope>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ScanResult(new ScanStatistics { FilesAdded = filesAdded, IsPartial = !full }, []));
        IJobService jobs = Substitute.For<IJobService>();
        IJobExecutionContext context = ContextFor(full ? null : ScanScope.Of(_source, ["Artist/Album"]));

        await CreateJob(scanner, jobs).Execute(context, CancellationToken.None);

        // The analysis looks up the lyrics in its order and starts the download of the rest itself.
        await jobs.Received(expected).StartRunAsync(
            new JobRunRequest(JobIds.SongAnalysis, JobRunTrigger.LibraryScan),
            Arg.Any<CancellationToken>());
        await jobs.DidNotReceive().StartRunAsync(
            new JobRunRequest(JobIds.LyricsDownload, JobRunTrigger.LibraryScan),
            Arg.Any<CancellationToken>());
    }

    [TestCase(true, 0)]
    [TestCase(false, 1)]
    [TestCase(false, 0)]
    public async Task Execute_AfterEveryScan_StartsTheLibraryCheck(bool full, int filesAdded)
    {
        ILibraryScanner scanner = Substitute.For<ILibraryScanner>();
        scanner.ScanAsync(Arg.Any<ScanScope>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new ScanResult(new ScanStatistics { FilesAdded = filesAdded, IsPartial = !full }, []));
        IJobService jobs = Substitute.For<IJobService>();
        IJobExecutionContext context = ContextFor(full ? null : ScanScope.Of(_source, ["Artist/Album"]));

        await CreateJob(scanner, jobs).Execute(context, CancellationToken.None);

        await jobs.Received(1).StartRunAsync(
            new JobRunRequest(JobIds.LibraryCheck, JobRunTrigger.LibraryScan),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Execute_PreparationCannotBeStarted_KeepsTheScanSuccessful()
    {
        ILibraryScanner scanner = Substitute.For<ILibraryScanner>();
        ScanResult result = new(new ScanStatistics(), []);
        scanner.ScanAsync(Arg.Any<ScanScope>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(result);
        IJobService jobs = Substitute.For<IJobService>();
        jobs.StartRunAsync(Arg.Any<JobRunRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database gone"));
        IJobExecutionContext context = ContextFor(null);

        Func<Task> execute = async () => await CreateJob(scanner, jobs).Execute(context, CancellationToken.None);

        await execute.Should().NotThrowAsync();
        context.Received().Result = result;
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not json")]
    [TestCase("[]")]
    [TestCase("[\"Artist\"]")]
    [TestCase("[{\"sourceId\":\"019a0000-0000-7000-8000-000000000001\"}]")]
    public void ReadScope_WithoutUsablePayload_IsAFullScan(string? payload)
    {
        JobDataMap data = payload is null ? [] : new JobDataMap { [JobDataKeys.Payload] = payload };

        LibraryScanJob.ReadScope(data).IsFull.Should().BeTrue();
    }

    [Test]
    public void ScheduleProvider_OnStartAndAfterChanges_UsesTheIntervalOfTheSettings()
    {
        ISettingsSnapshot<LibrarySettings> settings = Substitute.For<ISettingsSnapshot<LibrarySettings>>();
        settings.Current.Returns(new LibrarySettings { ScanIntervalMinutes = 30 });
        LibraryScanScheduleProvider provider = new(
            settings,
            Microsoft.Extensions.Options.Options.Create(new ScanOptions { StartupDelay = TimeSpan.FromMinutes(2) }));

        provider.JobId.Should().Be(JobIds.LibraryScan);
        provider.GetSchedule(JobScheduleReason.Startup)
            .Should().Be(new JobSchedule(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(2)));
        provider.GetSchedule(JobScheduleReason.SettingsChanged)
            .Should().Be(new JobSchedule(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30)));
    }

    [Test]
    public async Task Rescheduler_ChangedInterval_PlansTheScanAgain()
    {
        IJobScheduler scheduler = Substitute.For<IJobScheduler>();
        LibraryScanRescheduler rescheduler = new(scheduler);

        await rescheduler.OnSettingsChangedAsync(
            new LibrarySettings { ScanIntervalMinutes = 60 },
            new LibrarySettings { ScanIntervalMinutes = 30 },
            CancellationToken.None);

        await scheduler.Received(1).RescheduleAsync(JobIds.LibraryScan, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Rescheduler_OtherChanges_KeepTheSchedule()
    {
        IJobScheduler scheduler = Substitute.For<IJobScheduler>();
        LibraryScanRescheduler rescheduler = new(scheduler);

        await rescheduler.OnSettingsChangedAsync(
            new LibrarySettings { FileWatcherEnabled = true },
            new LibrarySettings { FileWatcherEnabled = false },
            CancellationToken.None);

        await scheduler.DidNotReceive().RescheduleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static LibraryScanJob CreateJob(
        ILibraryScanner scanner,
        IJobService jobs,
        JobRunTracker? tracker = null)
    {
        return new LibraryScanJob(scanner, tracker ?? new JobRunTracker(), jobs, NullLogger<LibraryScanJob>.Instance);
    }

    private static IJobExecutionContext ContextFor(ScanScope? scope)
    {
        IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
        context.FireInstanceId.Returns("fire-1");
        context.MergedJobDataMap.Returns(scope is null
            ? []
            : new JobDataMap { [JobDataKeys.Payload] = LibraryScanJob.CreatePayload(scope) });
        return context;
    }
}
