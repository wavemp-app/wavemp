using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Core.Scrobbling.Options;
using WaveMP.Core.Scrobbling.Queues;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scrobbling.Tests.Queues;

/// <summary>
/// The queue against a repository in memory and simulated services (NSubstitute).
/// </summary>
[TestFixture]
public class ScrobbleQueueProcessorTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private InMemoryScrobbleRepository _repository = null!;
    private IScrobbleSender _sender = null!;
    private ScrobblingOptions _options = null!;
    private ScrobbleQueueProcessor _processor = null!;
    private ScrobbleConnection _connection = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryScrobbleRepository();
        _connection = _repository.AddConnection(ScrobbleService.ListenBrainz, FakeProtector.Prefix + "token");
        _sender = Substitute.For<IScrobbleSender>();
        _options = new ScrobblingOptions
        {
            BatchSize = 2,
            RetryDelay = TimeSpan.FromMinutes(1),
            MaxRetryDelay = TimeSpan.FromMinutes(10),
        };
        TimeProvider time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(_now);
        _processor = new ScrobbleQueueProcessor(
            _repository,
            _sender,
            new FakeProtector(),
            time,
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<ScrobbleQueueProcessor>.Instance);
    }

    [Test]
    public async Task ProcessAsync_ServiceAccepts_SendsInBatchesInTheOrderOfPlayAndEmptiesTheQueue()
    {
        _repository.AddEntry(_connection, "Third", _now.AddMinutes(-10));
        _repository.AddEntry(_connection, "First", _now.AddMinutes(-30));
        _repository.AddEntry(_connection, "Second", _now.AddMinutes(-20));
        List<string[]> batches = [];
        _sender.SendScrobblesAsync(
                ScrobbleService.ListenBrainz,
                "token",
                Arg.Any<IReadOnlyList<ScrobblePlay>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                batches.Add([.. call.Arg<IReadOnlyList<ScrobblePlay>>().Select(play => play.Track.Title)]);
                return ScrobbleSendStatus.Accepted;
            });

        DateTimeOffset? next = await _processor.ProcessAsync();

        batches.Should().BeEquivalentTo(
            [new[] { "First", "Second" }, ["Third"]],
            options => options.WithStrictOrdering());
        _repository.Entries.Should().BeEmpty();
        _repository.Connections.Single().LastScrobbledAt.Should().Be(_now);
        next.Should().BeNull();
    }

    [Test]
    public async Task ProcessAsync_ServiceUnavailable_KeepsTheScrobblesAndTriesAgainLater()
    {
        _repository.AddEntry(_connection, "First", _now.AddMinutes(-30));
        _repository.AddEntry(_connection, "Second", _now.AddMinutes(-20));
        _repository.AddEntry(_connection, "Third", _now.AddMinutes(-10));
        Unavailable();

        DateTimeOffset? next = await _processor.ProcessAsync();

        _repository.Entries.Should().HaveCount(3).And.AllSatisfy(entry =>
        {
            entry.Attempts.Should().Be(1);
            entry.NextAttemptAt.Should().Be(_now.AddMinutes(1));
        });
        next.Should().Be(_now.AddMinutes(1));
        await _sender.ReceivedWithAnyArgs(1).SendScrobblesAsync(default, default!, default!, default);
    }

    [Test]
    public async Task ProcessAsync_ServiceUnavailableAgain_DoublesTheWaitUpToTheMaximum()
    {
        ScrobbleQueueEntry entry = _repository.AddEntry(_connection, "First", _now.AddMinutes(-30));
        entry.Attempts = 3;
        Unavailable();

        await _processor.ProcessAsync();

        entry.Attempts.Should().Be(4);
        entry.NextAttemptAt.Should().Be(_now.AddMinutes(8));
        ScrobbleQueueProcessor.RetryDelay(4, _options).Should().Be(TimeSpan.FromMinutes(10));
    }

    [Test]
    public async Task ProcessAsync_EntryNotDueYet_IsNotSent()
    {
        ScrobbleQueueEntry entry = _repository.AddEntry(_connection, "First", _now.AddMinutes(-30));
        entry.NextAttemptAt = _now.AddMinutes(5);

        DateTimeOffset? next = await _processor.ProcessAsync();

        await _sender.DidNotReceiveWithAnyArgs().SendScrobblesAsync(default, default!, default!, default);
        next.Should().Be(_now.AddMinutes(5));
    }

    [Test]
    public async Task ProcessAsync_CredentialsRejected_MarksTheConnectionAndKeepsTheScrobbles()
    {
        _repository.AddEntry(_connection, "First", _now.AddMinutes(-30));
        _sender.SendScrobblesAsync(default, default!, default!, default)
            .ReturnsForAnyArgs(ScrobbleSendStatus.Unauthorized);

        DateTimeOffset? next = await _processor.ProcessAsync();

        _repository.Connections.Single().Status.Should().Be(ScrobbleConnectionStatus.AuthorizationFailed);
        _repository.Entries.Should().ContainSingle();
        next.Should().BeNull("scrobbles of a connection that waits for the user are not due");
    }

    [Test]
    public async Task ProcessAsync_CredentialsCannotBeDecrypted_MarksTheConnection()
    {
        _connection.ProtectedCredential = "garbage";
        _repository.AddEntry(_connection, "First", _now.AddMinutes(-30));

        await _processor.ProcessAsync();

        _repository.Connections.Single().Status.Should().Be(ScrobbleConnectionStatus.AuthorizationFailed);
        await _sender.DidNotReceiveWithAnyArgs().SendScrobblesAsync(default, default!, default!, default);
    }

    [Test]
    public async Task ProcessAsync_BatchRejected_SendsEachOnItsOwnAndDropsOnlyTheBadOne()
    {
        _repository.AddEntry(_connection, "Good", _now.AddMinutes(-30));
        _repository.AddEntry(_connection, "Bad", _now.AddMinutes(-20));
        _sender.SendScrobblesAsync(default, default!, default!, default).ReturnsForAnyArgs(call =>
        {
            IReadOnlyList<ScrobblePlay> plays = call.Arg<IReadOnlyList<ScrobblePlay>>();
            return plays.Any(play => play.Track.Title == "Bad")
                ? ScrobbleSendStatus.Rejected
                : ScrobbleSendStatus.Accepted;
        });

        await _processor.ProcessAsync();

        _repository.Entries.Should().BeEmpty();
        await _sender.ReceivedWithAnyArgs(3).SendScrobblesAsync(default, default!, default!, default);
        _repository.Connections.Single().LastScrobbledAt.Should().Be(_now);
    }

    [Test]
    public async Task ProcessAsync_ExpiredScrobbles_AreDropped()
    {
        _repository.AddEntry(_connection, "Old", _now.AddDays(-15));
        _sender.SendScrobblesAsync(default, default!, default!, default).ReturnsForAnyArgs(ScrobbleSendStatus.Accepted);

        await _processor.ProcessAsync();

        _repository.Entries.Should().BeEmpty();
        await _sender.DidNotReceiveWithAnyArgs().SendScrobblesAsync(default, default!, default!, default);
    }

    [Test]
    public async Task ProcessAsync_OneServiceDown_StillSendsToTheOther()
    {
        ScrobbleConnection lastFm = _repository.AddConnection(ScrobbleService.LastFm, FakeProtector.Prefix + "key");
        _repository.AddEntry(_connection, "Song", _now.AddMinutes(-5));
        _repository.AddEntry(lastFm, "Song", _now.AddMinutes(-5));
        _sender.SendScrobblesAsync(ScrobbleService.ListenBrainz, default!, default!, default)
            .ReturnsForAnyArgs(call => call.Arg<ScrobbleService>() == ScrobbleService.LastFm
                ? ScrobbleSendStatus.Accepted
                : ScrobbleSendStatus.Unavailable);

        await _processor.ProcessAsync();

        _repository.Entries.Should().ContainSingle().Which.ConnectionId.Should().Be(_connection.Id);
    }

    private void Unavailable()
    {
        _sender.SendScrobblesAsync(default, default!, default!, default)
            .ReturnsForAnyArgs(ScrobbleSendStatus.Unavailable);
    }
}
