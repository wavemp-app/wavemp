using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Library.Reviews;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Tests.Reviews;

[TestFixture]
public class TrackReviewServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    private readonly Guid _trackId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private ITrackReviewRepository _reviews = null!;
    private TrackReviewService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _reviews = Substitute.For<ITrackReviewRepository>();
        _reviews.ListAsync(Arg.Any<TrackReviewListQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Page<TrackReviewRow>([], 0, 10, 0));
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_now);
        _service = new TrackReviewService(_reviews, clock, NullLogger<TrackReviewService>.Instance);
    }

    [Test]
    public async Task ListAsync_WithoutOptions_ListsTheUnreviewedTracksNewestFirst()
    {
        await _service.ListAsync(new TrackReviewListRequest(20, 10, Search: "   "));

        await _reviews.Received(1).ListAsync(
            new TrackReviewListQuery(
                20,
                10,
                TrackReviewSortField.FirstSeenAt,
                SortDirection.Desc,
                TrackReviewStatus.Unreviewed,
                null,
                null),
            Arg.Any<CancellationToken>());
    }

    [TestCase(TrackReviewSortField.Artist)]
    [TestCase(TrackReviewSortField.Album)]
    [TestCase(TrackReviewSortField.Title)]
    public async Task ListAsync_OtherSortKey_SortsAscendingByDefault(TrackReviewSortField sort)
    {
        await _service.ListAsync(new TrackReviewListRequest(0, 10, sort));

        await _reviews.Received(1).ListAsync(
            Arg.Is<TrackReviewListQuery>(query => query.Sort == sort && query.Direction == SortDirection.Asc),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListAsync_WithOptions_PassesThemOn()
    {
        await _service.ListAsync(new TrackReviewListRequest(
            0,
            10,
            TrackReviewSortField.Album,
            SortDirection.Desc,
            TrackReviewStatus.Reviewed,
            true,
            "  rock "));

        await _reviews.Received(1).ListAsync(
            new TrackReviewListQuery(
                0,
                10,
                TrackReviewSortField.Album,
                SortDirection.Desc,
                TrackReviewStatus.Reviewed,
                true,
                "rock"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CountAsync_Always_ReturnsTheCountsOfTheRepository()
    {
        _reviews.CountAsync(Arg.Any<CancellationToken>()).Returns(new TrackReviewCounts(7, 3));

        TrackReviewCounts counts = await _service.CountAsync();

        counts.Should().Be(new TrackReviewCounts(7, 3));
    }

    [Test]
    public async Task SetAsync_Reviewed_MarksTheTrackByTheProfileNow()
    {
        TrackReviewStateRow state = new(_trackId, _now, _userId, "Admin");
        _reviews.FindAsync(_trackId, Arg.Any<CancellationToken>()).Returns(state);

        TrackReviewStateRow? result = await _service.SetAsync(_trackId, TrackReviewStatus.Reviewed, _userId);

        result.Should().Be(state);
        await _reviews.Received(1).MarkReviewedAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { _trackId })),
            _userId,
            _now,
            Arg.Any<CancellationToken>());
        await _reviews.DidNotReceiveWithAnyArgs().MarkUnreviewedAsync(default!, default);
    }

    [Test]
    public async Task SetAsync_Unreviewed_PutsTheTrackBackIntoTheInbox()
    {
        TrackReviewStateRow state = new(_trackId, null, null, null);
        _reviews.FindAsync(_trackId, Arg.Any<CancellationToken>()).Returns(state);

        TrackReviewStateRow? result = await _service.SetAsync(_trackId, TrackReviewStatus.Unreviewed, _userId);

        result.Should().Be(state);
        result!.IsReviewed.Should().BeFalse();
        await _reviews.Received(1).MarkUnreviewedAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { _trackId })),
            Arg.Any<CancellationToken>());
        await _reviews.DidNotReceiveWithAnyArgs().MarkReviewedAsync(default!, default, default, default);
    }

    [Test]
    public async Task SetAsync_UnknownTrack_ReturnsNull()
    {
        _reviews.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TrackReviewStateRow?)null);

        TrackReviewStateRow? result = await _service.SetAsync(Guid.NewGuid(), TrackReviewStatus.Reviewed, _userId);

        result.Should().BeNull();
    }

    [Test]
    public async Task SetManyAsync_Duplicates_MarksEachTrackOnceAndReturnsTheChanges()
    {
        var other = Guid.NewGuid();
        _reviews.MarkReviewedAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(2);

        int changed = await _service.SetManyAsync([_trackId, other, _trackId], TrackReviewStatus.Reviewed, _userId);

        changed.Should().Be(2);
        await _reviews.Received(1).MarkReviewedAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { _trackId, other })),
            _userId,
            _now,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetManyAsync_NoTracks_ChangesNothing()
    {
        int changed = await _service.SetManyAsync([], TrackReviewStatus.Reviewed, _userId);

        changed.Should().Be(0);
        await _reviews.DidNotReceiveWithAnyArgs().MarkReviewedAsync(default!, default, default, default);
    }

    [Test]
    public async Task SetManyAsync_TooManyTracks_Throws()
    {
        Guid[] ids = [.. Enumerable.Range(0, TrackReviews.MaxBatchSize + 1).Select(_ => Guid.NewGuid())];

        Func<Task> act = () => _service.SetManyAsync(ids, TrackReviewStatus.Reviewed, _userId);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
