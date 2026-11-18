using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Scrobbling.Options;
using WaveMP.Core.Scrobbling.Queues;
using WaveMP.Core.Scrobbling.Tests.Clients;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scrobbling.Tests.Queues;

[TestFixture]
public class ScrobbleQueueTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _userId = Guid.NewGuid();

    private IScrobbleRepository _repository = null!;
    private ScrobbleQueueSignal _signal = null!;
    private ScrobbleQueue _queue = null!;
    private List<ScrobbleQueueEntry> _queued = null!;

    [SetUp]
    public void SetUp()
    {
        _queued = [];
        _repository = Substitute.For<IScrobbleRepository>();
        _repository.ListConnectionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns([Connection(ScrobbleService.LastFm), Connection(ScrobbleService.ListenBrainz)]);
        _repository.EnqueueAsync(Arg.Any<IReadOnlyCollection<ScrobbleQueueEntry>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _queued.AddRange(call.Arg<IReadOnlyCollection<ScrobbleQueueEntry>>());
                return call.Arg<IReadOnlyCollection<ScrobbleQueueEntry>>().Count;
            });
        _signal = new ScrobbleQueueSignal();
        TimeProvider time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(_now);
        _queue = new ScrobbleQueue(
            _repository,
            _signal,
            time,
            Microsoft.Extensions.Options.Options.Create(new ScrobblingOptions()));
    }

    [TearDown]
    public void TearDown()
    {
        _signal.Dispose();
    }

    [Test]
    public async Task EnqueueAsync_SongOfTheUser_QueuesItForEveryConnectionAndWakesTheSender()
    {
        TrackRow track = ScrobbleTracksTests.Track("Title", "Artist", "Album", null) with { TrackNumber = 2 };
        DateTimeOffset playedAt = _now.AddMinutes(-3);

        int added = await _queue.EnqueueAsync(_userId, track, playedAt);

        added.Should().Be(2);
        _queued.Should().HaveCount(2).And.AllSatisfy(entry => entry.Should().BeEquivalentTo(new
        {
            Artist = "Artist",
            Title = "Title",
            Album = "Album",
            AlbumArtist = (string?)null,
            TrackNumber = 2,
            Duration = TimeSpan.FromMinutes(3),
            PlayedAt = playedAt,
            Attempts = 0,
            NextAttemptAt = _now,
        }));
        _queued.Select(entry => entry.ConnectionId).Should().OnlyHaveUniqueItems();
        (await _signal.WaitAsync(TimeSpan.Zero, CancellationToken.None)).Should().BeTrue();
    }

    [Test]
    public async Task EnqueueAsync_SongShorterThanThirtySeconds_QueuesNothing()
    {
        TrackRow track = ScrobbleTracksTests.Track("Jingle", "Artist", null, null) with
        {
            Duration = TimeSpan.FromSeconds(29),
        };

        int added = await _queue.EnqueueAsync(_userId, track, _now);

        added.Should().Be(0);
        await _repository.DidNotReceiveWithAnyArgs().EnqueueAsync(default!, default);
    }

    [Test]
    public async Task EnqueueAsync_SongWithoutArtist_QueuesNothing()
    {
        int added = await _queue.EnqueueAsync(_userId, ScrobbleTracksTests.Track("Title", null, null, null), _now);

        added.Should().Be(0);
    }

    [Test]
    public async Task EnqueueAsync_PlayOlderThanTheServicesAccept_QueuesNothing()
    {
        TrackRow track = ScrobbleTracksTests.Track("Title", "Artist", null, null);

        int added = await _queue.EnqueueAsync(_userId, track, _now.AddDays(-15));

        added.Should().Be(0);
    }

    [Test]
    public async Task EnqueueAsync_NoConnection_QueuesNothingAndDoesNotWakeTheSender()
    {
        _repository.ListConnectionsAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);

        int added = await _queue.EnqueueAsync(_userId, ScrobbleTracksTests.Track("Title", "Artist", null, null), _now);

        added.Should().Be(0);
        (await _signal.WaitAsync(TimeSpan.Zero, CancellationToken.None)).Should().BeFalse();
    }

    private ScrobbleConnection Connection(ScrobbleService service)
    {
        return new ScrobbleConnection
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Service = service,
            AccountName = "listener",
            ProtectedCredential = FakeProtector.Prefix + "token",
            Status = ScrobbleConnectionStatus.Active,
            ConnectedAt = _now,
        };
    }
}
