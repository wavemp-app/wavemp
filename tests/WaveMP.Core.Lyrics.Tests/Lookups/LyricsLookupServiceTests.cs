using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Lyrics.Lookups;
using WaveMP.Core.Lyrics.Options;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Tests.Lookups;

[TestFixture]
public class LyricsLookupServiceTests
{
    private static readonly Guid _trackId = Guid.NewGuid();

    private static readonly LyricsDownloadStatus[] _forgettable =
        [LyricsDownloadStatus.NotFound, LyricsDownloadStatus.Instrumental];

    private ILyricsRepository _repository = null!;
    private LyricsOptions _options = null!;
    private LyricsLookupService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ILyricsRepository>();
        _options = new LyricsOptions();
        _service = new LyricsLookupService(
            _repository,
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<LyricsLookupService>.Instance);
    }

    [Test]
    public async Task GetSummaryAsync_Results_CountsOnlyLookupsWithoutLyricsAndNamesTheWait()
    {
        _repository.CountDownloadsAsync(Arg.Any<CancellationToken>()).Returns(
            new Dictionary<LyricsDownloadStatus, int>
            {
                [LyricsDownloadStatus.NotFound] = 7,
                [LyricsDownloadStatus.Instrumental] = 2,
                [LyricsDownloadStatus.SavedToFile] = 40,
                [LyricsDownloadStatus.NotNeeded] = 3,
            });

        LyricsLookupSummary summary = await _service.GetSummaryAsync();

        summary.Should().Be(new LyricsLookupSummary(7, 2, TimeSpan.FromDays(30)));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task GetSummaryAsync_NoWait_HasNoWaitAndCountsZeroWithoutResults(int days)
    {
        _options.LookUpAgainAfter = TimeSpan.FromDays(days);
        _repository.CountDownloadsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<LyricsDownloadStatus, int>());

        LyricsLookupSummary summary = await _service.GetSummaryAsync();

        summary.Should().Be(new LyricsLookupSummary(0, 0, null));
    }

    [Test]
    public async Task GetAsync_TrackWithOrWithoutResult_ReturnsTheResultOrWhyThereIsNone()
    {
        LyricsDownloadRow lookup = new(LyricsDownloadStatus.Instrumental, null, DateTimeOffset.UnixEpoch);
        var withoutResult = Guid.NewGuid();
        Track(_trackId, lookup);
        Track(withoutResult, null);

        LyricsLookupResult found = await _service.GetAsync(_trackId);

        found.IsFound.Should().BeTrue();
        found.Lookup.Should().Be(lookup);
        found.IsForgettable.Should().BeTrue();
        (await _service.GetAsync(withoutResult)).Should().Be(LyricsLookupResult.NotLookedUp);
        (await _service.GetAsync(Guid.NewGuid())).Should().Be(LyricsLookupResult.TrackNotFound);
    }

    [TestCase(LyricsDownloadStatus.NotFound, true)]
    [TestCase(LyricsDownloadStatus.Instrumental, true)]
    [TestCase(LyricsDownloadStatus.SavedToFile, false)]
    [TestCase(LyricsDownloadStatus.SavedToDatabase, false)]
    [TestCase(LyricsDownloadStatus.NotNeeded, false)]
    public void IsForgettableStatus_Status_IsTrueOnlyForLookupsWithoutLyrics(
        LyricsDownloadStatus status,
        bool expected)
    {
        LyricsLookupResult.IsForgettableStatus(status).Should().Be(expected);
    }

    [Test]
    public async Task ForgetAllAsync_Always_RemovesTheLookupsWithoutLyricsOfAllTracks()
    {
        _repository.DeleteDownloadsAsync(
                Arg.Any<IReadOnlyCollection<LyricsDownloadStatus>>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(5);

        int forgotten = await _service.ForgetAllAsync();

        forgotten.Should().Be(5);
        await _repository.Received(1).DeleteDownloadsAsync(
            Arg.Is<IReadOnlyCollection<LyricsDownloadStatus>>(statuses =>
                statuses.Order().SequenceEqual(_forgettable)),
            null,
            Arg.Any<CancellationToken>());
    }

    [TestCase(LyricsDownloadStatus.NotFound)]
    [TestCase(LyricsDownloadStatus.Instrumental)]
    public async Task ForgetAsync_LookupWithoutLyrics_RemovesItsResult(LyricsDownloadStatus status)
    {
        Track(_trackId, new LyricsDownloadRow(status, null, DateTimeOffset.UnixEpoch));
        _repository.DeleteDownloadsAsync(
                Arg.Any<IReadOnlyCollection<LyricsDownloadStatus>>(),
                _trackId,
                Arg.Any<CancellationToken>())
            .Returns(1);

        LyricsLookupForgetOutcome outcome = await _service.ForgetAsync(_trackId);

        outcome.Should().Be(LyricsLookupForgetOutcome.Forgotten);
        await _repository.Received(1).DeleteDownloadsAsync(
            Arg.Is<IReadOnlyCollection<LyricsDownloadStatus>>(statuses =>
                statuses.Order().SequenceEqual(_forgettable)),
            _trackId,
            Arg.Any<CancellationToken>());
    }

    [TestCase(LyricsDownloadStatus.SavedToFile)]
    [TestCase(LyricsDownloadStatus.SavedToDatabase)]
    [TestCase(LyricsDownloadStatus.NotNeeded)]
    public async Task ForgetAsync_LookupWithLyrics_KeepsIt(LyricsDownloadStatus status)
    {
        Track(_trackId, new LyricsDownloadRow(status, "Line", DateTimeOffset.UnixEpoch));

        LyricsLookupForgetOutcome outcome = await _service.ForgetAsync(_trackId);

        outcome.Should().Be(LyricsLookupForgetOutcome.FoundLyrics);
        await _repository.DidNotReceiveWithAnyArgs().DeleteDownloadsAsync(default!, default, default);
    }

    [Test]
    public async Task ForgetAsync_NoResultOrUnknownTrack_ForgetsNothing()
    {
        Track(_trackId, null);

        (await _service.ForgetAsync(_trackId)).Should().Be(LyricsLookupForgetOutcome.NotLookedUp);
        (await _service.ForgetAsync(Guid.NewGuid())).Should().Be(LyricsLookupForgetOutcome.TrackNotFound);
        await _repository.DidNotReceiveWithAnyArgs().DeleteDownloadsAsync(default!, default, default);
    }

    [Test]
    public async Task ForgetAsync_DownloadReplacedTheResultInTheMeantime_KeepsTheNewResult()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(
            Row(_trackId, new LyricsDownloadRow(LyricsDownloadStatus.NotFound, null, DateTimeOffset.UnixEpoch)),
            Row(_trackId, new LyricsDownloadRow(LyricsDownloadStatus.SavedToFile, null, DateTimeOffset.UtcNow)));

        LyricsLookupForgetOutcome outcome = await _service.ForgetAsync(_trackId);

        outcome.Should().Be(LyricsLookupForgetOutcome.FoundLyrics);
    }

    [Test]
    public async Task ForgetAsync_ResultForgottenInTheMeantime_ReportsNoResult()
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(
            Row(_trackId, new LyricsDownloadRow(LyricsDownloadStatus.NotFound, null, DateTimeOffset.UnixEpoch)),
            Row(_trackId, null));

        LyricsLookupForgetOutcome outcome = await _service.ForgetAsync(_trackId);

        outcome.Should().Be(LyricsLookupForgetOutcome.NotLookedUp);
    }

    private void Track(Guid trackId, LyricsDownloadRow? download)
    {
        _repository.FindTrackAsync(trackId, Arg.Any<CancellationToken>()).Returns(Row(trackId, download));
    }

    private static LyricsTrackRow Row(Guid trackId, LyricsDownloadRow? download)
    {
        return new LyricsTrackRow(
            trackId,
            MediaSource.DefaultId,
            "Band",
            "Song",
            "Song",
            "Band",
            "Album",
            TimeSpan.FromMinutes(3),
            null,
            "Band/Song.mp3",
            null,
            download);
    }
}
