using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Recaps.Shares;
using WaveMP.Core.Recaps.Statistics;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Recaps.Tests.Shares;

[TestFixture]
public class RecapShareServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _owner = Guid.Parse("00000000-0000-0000-0000-0000000000f1");
    private static readonly Guid _other = Guid.Parse("00000000-0000-0000-0000-0000000000f2");

    private IRecapShareRepository _shares = null!;
    private IListeningStatisticsService _statistics = null!;
    private RecapShareService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _shares = Substitute.For<IRecapShareRepository>();
        _statistics = Substitute.For<IListeningStatisticsService>();
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_now);
        _service = new RecapShareService(_shares, _statistics, clock);
    }

    [Test]
    public async Task ShareAsync_Always_SavesThePeriodAndDesignWithTheCurrentTime()
    {
        RecapShare? saved = null;
        _shares.SaveAsync(Arg.Do<RecapShare>(share => saved = share), Arg.Any<CancellationToken>())
            .Returns(call => new RecapShareSaveResult(new RecapShareRow(call.Arg<RecapShare>(), "Owner"), true));

        RecapShareSaveResult result = await _service.ShareAsync(new RecapShareRequest(
            _owner,
            RecapPeriod.CurrentYear,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 11, 20),
            "Europe/Berlin",
            "prism"));

        result.Created.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(_owner);
        saved.Period.Should().Be(RecapPeriod.CurrentYear);
        saved.FromDate.Should().Be(new DateOnly(2026, 1, 1));
        saved.ToDate.Should().Be(new DateOnly(2026, 11, 20));
        saved.TimeZone.Should().Be("Europe/Berlin");
        saved.Design.Should().Be("prism");
        saved.CreatedAt.Should().Be(_now);
        saved.UpdatedAt.Should().Be(_now);
        saved.Id.Should().NotBe(Guid.Empty);
    }

    [Test]
    public async Task DeleteAsync_ShareOfTheUser_DeletesIt()
    {
        RecapShareRow row = Row(_owner, "UTC");
        _shares.FindAsync(row.Share.Id, Arg.Any<CancellationToken>()).Returns(row);
        _shares.DeleteAsync(_owner, row.Share.Id, Arg.Any<CancellationToken>()).Returns(true);

        RecapShareDeletion result = await _service.DeleteAsync(_owner, row.Share.Id);

        result.Should().Be(RecapShareDeletion.Deleted);
    }

    [Test]
    public async Task DeleteAsync_ShareOfAnotherUser_KeepsItAndReturnsNotOwner()
    {
        RecapShareRow row = Row(_owner, "UTC");
        _shares.FindAsync(row.Share.Id, Arg.Any<CancellationToken>()).Returns(row);

        RecapShareDeletion result = await _service.DeleteAsync(_other, row.Share.Id);

        result.Should().Be(RecapShareDeletion.NotOwner);
        await _shares.DidNotReceiveWithAnyArgs().DeleteAsync(default, default, default);
    }

    [Test]
    public async Task DeleteAsync_UnknownShare_ReturnsNotFound()
    {
        RecapShareDeletion result = await _service.DeleteAsync(_owner, Guid.NewGuid());

        result.Should().Be(RecapShareDeletion.NotFound);
    }

    [Test]
    public async Task GetStatisticsAsync_Always_CountsThePlaysOfTheOwnerForTheViewer()
    {
        RecapShareRow row = Row(_owner, "Europe/Berlin");
        _shares.FindAsync(row.Share.Id, Arg.Any<CancellationToken>()).Returns(row);
        ListeningStatistics statistics = Empty();
        _statistics.GetAsync(Arg.Any<StatisticsQuery>(), Arg.Any<CancellationToken>()).Returns(statistics);

        SharedRecap? result = await _service.GetStatisticsAsync(row.Share.Id, _other, 5);

        result.Should().Be(new SharedRecap(row, statistics));
        await _statistics.Received(1).GetAsync(
            Arg.Is<StatisticsQuery>(query => query.UserId == _owner
                && query.ViewerId == _other
                && query.From == row.Share.FromDate
                && query.To == row.Share.ToDate
                && query.Top == 5
                && query.TimeZone.Id == "Europe/Berlin"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetStatisticsAsync_UnknownTimeZone_ReturnsNull()
    {
        RecapShareRow row = Row(_owner, "Mars/Olympus_Mons");
        _shares.FindAsync(row.Share.Id, Arg.Any<CancellationToken>()).Returns(row);

        SharedRecap? result = await _service.GetStatisticsAsync(row.Share.Id, _other, 5);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetStatisticsAsync_UnknownShare_ReturnsNull()
    {
        SharedRecap? result = await _service.GetStatisticsAsync(Guid.NewGuid(), _other, 5);

        result.Should().BeNull();
    }

    private static RecapShareRow Row(Guid userId, string timeZone)
    {
        return new RecapShareRow(
            new RecapShare
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Period = RecapPeriod.LastMonth,
                FromDate = new DateOnly(2026, 10, 21),
                ToDate = new DateOnly(2026, 11, 20),
                TimeZone = timeZone,
                Design = "aurora",
                CreatedAt = _now,
                UpdatedAt = _now,
            },
            "Owner");
    }

    private static ListeningStatistics Empty()
    {
        return new ListeningStatistics(
            new DateOnly(2026, 10, 21),
            new DateOnly(2026, 11, 20),
            "Europe/Berlin",
            new StatisticsTotals(0, TimeSpan.Zero, 0, 0, 0, 0, 0, 0),
            [],
            [],
            [],
            [],
            [],
            new Timeline(TimelineGranularity.Day, []),
            []);
    }
}
