using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Jobs;
using WaveMP.Core.Lyrics.Options;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Tests.Jobs;

[TestFixture]
public class LyricsDownloadRunnerTests
{
    private ILyricsRepository _repository = null!;
    private ILyricsDownloader _downloader = null!;
    private ISettingsSnapshot<LibrarySettings> _settings = null!;
    private LyricsDownloadRunner _runner = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ILyricsRepository>();
        _repository.ListTracksToLookUpAsync(
                Arg.Any<int>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns([]);
        _downloader = Substitute.For<ILyricsDownloader>();
        _settings = Substitute.For<ISettingsSnapshot<LibrarySettings>>();
        _settings.Current.Returns(new LibrarySettings { DownloadLyrics = true });
        _runner = Runner(new LyricsOptions { MaxAttempts = 3, RetryDelay = TimeSpan.Zero }, TimeProvider.System);
    }

    [Test]
    public async Task RunAsync_SwitchedOff_Fails()
    {
        _settings.Current.Returns(new LibrarySettings());

        Func<Task> act = () => _runner.RunAsync();

        (await act.Should().ThrowAsync<JobFailedException>())
            .Which.Code.Should().Be(JobErrorCode.LyricsDownloadDisabled);
    }

    [Test]
    public async Task RunAsync_Tracks_DownloadsEachOnceAndCountsTheOutcomes()
    {
        Guid[] ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        Batches([ids[0], ids[1]], [ids[2], ids[3]]);
        Outcome(ids[0], LyricsDownloadOutcome.SavedToFile);
        Outcome(ids[1], LyricsDownloadOutcome.NotFound);
        Outcome(ids[2], LyricsDownloadOutcome.SavedToDatabase);
        Outcome(ids[3], LyricsDownloadOutcome.NotNeeded);

        LyricsDownloadJobResult result = await _runner.RunAsync();

        result.Should().Be(new LyricsDownloadJobResult(4, 2, 1, 1, false));
        foreach (Guid id in ids)
        {
            await _downloader.Received(1).DownloadAsync(id, LyricsDownloadMode.Automatic, Arg.Any<CancellationToken>());
        }

        await _repository.Received().ListTracksToLookUpAsync(
            LyricsDownloadRunner.BatchSize,
            Arg.Is<IReadOnlyCollection<Guid>>(excluded => excluded.Count == 4),
            Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_ServiceBusyOnce_TriesTheTrackAgain()
    {
        var id = Guid.NewGuid();
        Batches([id]);
        _downloader.DownloadAsync(id, LyricsDownloadMode.Automatic, Arg.Any<CancellationToken>())
            .Returns(
                new LyricsDownloadResult(LyricsDownloadOutcome.RateLimited),
                new LyricsDownloadResult(LyricsDownloadOutcome.SavedToFile));

        LyricsDownloadJobResult result = await _runner.RunAsync();

        result.Downloaded.Should().Be(1);
        await _downloader.Received(2).DownloadAsync(id, LyricsDownloadMode.Automatic, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_ServiceUnavailable_FailsAfterTheLastAttempt()
    {
        var id = Guid.NewGuid();
        Batches([id]);
        Outcome(id, LyricsDownloadOutcome.Unavailable);

        Func<Task> act = () => _runner.RunAsync();

        (await act.Should().ThrowAsync<JobFailedException>())
            .Which.Code.Should().Be(JobErrorCode.LyricsServiceUnavailable);
        await _downloader.Received(3).DownloadAsync(id, LyricsDownloadMode.Automatic, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_SwitchedOffWhileRunning_Stops()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Batches([first, second]);
        Outcome(first, LyricsDownloadOutcome.NotFound);
        _settings.Current.Returns(
            new LibrarySettings { DownloadLyrics = true },
            new LibrarySettings { DownloadLyrics = true },
            new LibrarySettings());

        LyricsDownloadJobResult result = await _runner.RunAsync();

        result.Should().Be(new LyricsDownloadJobResult(1, 0, 1, 0, true));
        await _downloader.DidNotReceive().DownloadAsync(second, Arg.Any<LyricsDownloadMode>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_LookUpAgainAfter_AsksForTracksWhoseLookupFoundNothingBeforeThen()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);
        LyricsDownloadRunner runner = Runner(new LyricsOptions { LookUpAgainAfter = TimeSpan.FromDays(30) }, clock);

        await runner.RunAsync();

        await _repository.Received(1).ListTracksToLookUpAsync(
            LyricsDownloadRunner.BatchSize,
            Arg.Any<IReadOnlyCollection<Guid>>(),
            now.AddDays(-30),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_LookUpAgainSwitchedOff_AsksForNewTracksOnly()
    {
        LyricsDownloadRunner runner = Runner(new LyricsOptions { LookUpAgainAfter = TimeSpan.Zero }, TimeProvider.System);

        await runner.RunAsync();

        await _repository.Received(1).ListTracksToLookUpAsync(
            LyricsDownloadRunner.BatchSize,
            Arg.Any<IReadOnlyCollection<Guid>>(),
            null,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_TrackLookedUpAgain_IsDownloadedOnceAndCounted()
    {
        var id = Guid.NewGuid();
        IReadOnlyList<LyricsTrackRow> again =
        [
            Row(id) with
            {
                Download = new LyricsDownloadRow(LyricsDownloadStatus.NotFound, null, DateTimeOffset.UnixEpoch),
            },
        ];
        IReadOnlyList<LyricsTrackRow> none = [];
        _repository.ListTracksToLookUpAsync(
                Arg.Any<int>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(again, none);
        Outcome(id, LyricsDownloadOutcome.SavedToFile);

        LyricsDownloadJobResult result = await _runner.RunAsync();

        result.Should().Be(new LyricsDownloadJobResult(1, 1, 0, 0, false));
        await _downloader.Received(1).DownloadAsync(id, LyricsDownloadMode.Automatic, Arg.Any<CancellationToken>());
    }

    [TestCase(30, "2026-10-19T12:00:00Z")]
    [TestCase(1, "2026-11-17T12:00:00Z")]
    public void LookUpAgainBefore_Wait_IsTheTimeThatLongAgo(int days, string expected)
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

        LyricsDownloadRunner.LookUpAgainBefore(now, TimeSpan.FromDays(days))
            .Should().Be(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test]
    public void LookUpAgainBefore_NoOrEndlessWait_IsNull()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

        LyricsDownloadRunner.LookUpAgainBefore(now, TimeSpan.Zero).Should().BeNull();
        LyricsDownloadRunner.LookUpAgainBefore(now, TimeSpan.FromDays(-1)).Should().BeNull();
        LyricsDownloadRunner.LookUpAgainBefore(now, TimeSpan.MaxValue).Should().BeNull();
    }

    [Test]
    public void LookUpAgainAfter_Default_IsThirtyDays()
    {
        new LyricsOptions().LookUpAgainAfter.Should().Be(TimeSpan.FromDays(30));
    }

    [TestCase(null, 30)]
    [TestCase(10, 10)]
    [TestCase(0, 30)]
    [TestCase(3600, 300)]
    public void RetryDelay_RequestedWait_IsUsedWithinTheLimit(int? requestedSeconds, int expectedSeconds)
    {
        TimeSpan? requested = requestedSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null;

        LyricsDownloadRunner.RetryDelay(requested, new LyricsOptions())
            .Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    private void Batches(params Guid[][] batches)
    {
        IReadOnlyList<LyricsTrackRow>[] rows = [.. batches.Select(batch => (IReadOnlyList<LyricsTrackRow>)[.. batch.Select(Row)])];
        _repository.ListTracksToLookUpAsync(
                Arg.Any<int>(),
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(rows[0], [.. rows.Skip(1), []]);
    }

    private LyricsDownloadRunner Runner(LyricsOptions options, TimeProvider timeProvider)
    {
        return new LyricsDownloadRunner(
            _repository,
            _downloader,
            _settings,
            Microsoft.Extensions.Options.Options.Create(options),
            timeProvider,
            NullLogger<LyricsDownloadRunner>.Instance);
    }

    private void Outcome(Guid id, LyricsDownloadOutcome outcome)
    {
        _downloader.DownloadAsync(id, LyricsDownloadMode.Automatic, Arg.Any<CancellationToken>())
            .Returns(new LyricsDownloadResult(outcome));
    }

    private static LyricsTrackRow Row(Guid id)
    {
        return new LyricsTrackRow(id, "Band", "Song", "Song", "Band", "Album", null, null, "Band/Song.mp3", null, null);
    }
}
