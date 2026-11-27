using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class ScrobbleRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    protected override string DatabaseName => "wavemp_tests_data_scrobbles";

    [Test]
    public async Task SaveConnectionAsync_NewService_AddsTheConnection()
    {
        User user = await AddUserAsync();

        ScrobbleConnection saved = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.ListenBrainz, "listener"));

        IReadOnlyList<ScrobbleConnection> connections = await CreateRepository().ListConnectionsAsync(user.Id);
        connections.Should().ContainSingle().Which.Should().BeEquivalentTo(saved);
    }

    [Test]
    public async Task SaveConnectionAsync_ServiceConnectedBefore_KeepsTheIdAndTheQueue()
    {
        User user = await AddUserAsync();
        ScrobbleConnection first = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.LastFm, "old"));
        await CreateRepository().SetConnectionStatusAsync(first.Id, ScrobbleConnectionStatus.AuthorizationFailed);
        await CreateRepository().EnqueueAsync([Entry(first, _now.AddMinutes(-5))]);

        ScrobbleConnection second = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.LastFm, "new"));

        second.Id.Should().Be(first.Id);
        second.AccountName.Should().Be("new");
        second.Status.Should().Be(ScrobbleConnectionStatus.Active);
        (await CreateRepository().ListConnectionsAsync(user.Id)).Should().ContainSingle();
        (await CreateRepository().CountQueuedAsync(user.Id)).Should().Equal(
            new Dictionary<Guid, int> { [first.Id] = 1 });
    }

    [Test]
    public async Task SaveConnectionAsync_UnknownUser_Throws()
    {
        User user = await AddUserAsync();
        ScrobbleConnection connection = Connection(user, ScrobbleService.LastFm, "listener");
        connection.UserId = Guid.NewGuid();

        Func<Task> save = () => CreateRepository().SaveConnectionAsync(connection);

        await save.Should().ThrowAsync<UserNotFoundException>();
    }

    [Test]
    public async Task DeleteConnectionAsync_Connected_RemovesTheConnectionWithItsQueue()
    {
        User user = await AddUserAsync();
        ScrobbleConnection connection = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.LastFm, "listener"));
        await CreateRepository().EnqueueAsync([Entry(connection, _now)]);

        bool deleted = await CreateRepository().DeleteConnectionAsync(user.Id, ScrobbleService.LastFm);
        bool again = await CreateRepository().DeleteConnectionAsync(user.Id, ScrobbleService.LastFm);

        deleted.Should().BeTrue();
        again.Should().BeFalse();
        await using WaveMpDbContext dbContext = CreateDbContext();
        (await dbContext.ScrobbleQueue.CountAsync(entry => entry.ConnectionId == connection.Id)).Should().Be(0);
    }

    [Test]
    public async Task EnqueueAsync_ConnectionGone_LeavesItsEntriesOut()
    {
        User user = await AddUserAsync();
        ScrobbleConnection connection = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.ListenBrainz, "listener"));
        ScrobbleConnection gone = Connection(user, ScrobbleService.LastFm, "gone");

        int added = await CreateRepository().EnqueueAsync([Entry(connection, _now), Entry(gone, _now)]);

        added.Should().Be(1);
    }

    [Test]
    public async Task ListDueEntriesAsync_Always_ReturnsTheDueEntriesOfTheConnectionInTheOrderOfPlay()
    {
        User user = await AddUserAsync();
        ScrobbleConnection connection = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.ListenBrainz, "listener"));
        ScrobbleQueueEntry later = Entry(connection, _now.AddMinutes(-5));
        ScrobbleQueueEntry earlier = Entry(connection, _now.AddMinutes(-10));
        ScrobbleQueueEntry notDue = Entry(connection, _now.AddMinutes(-20));
        notDue.NextAttemptAt = _now.AddMinutes(1);
        await CreateRepository().EnqueueAsync([later, earlier, notDue]);

        IReadOnlyList<ScrobbleQueueEntry> due = await CreateRepository().ListDueEntriesAsync(connection.Id, _now, 10);
        IReadOnlyList<ScrobbleConnection> connections = await CreateRepository().ListDueConnectionsAsync(_now);

        due.Select(entry => entry.Id).Should().Equal(earlier.Id, later.Id);
        connections.Should().Contain(stored => stored.Id == connection.Id);
    }

    [Test]
    public async Task ListDueConnectionsAsync_AuthorizationFailed_LeavesTheConnectionOut()
    {
        User user = await AddUserAsync();
        ScrobbleConnection connection = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.ListenBrainz, "listener"));
        await CreateRepository().EnqueueAsync([Entry(connection, _now.AddMinutes(-5))]);
        await CreateRepository().SetConnectionStatusAsync(
            connection.Id,
            ScrobbleConnectionStatus.AuthorizationFailed);

        IReadOnlyList<ScrobbleConnection> connections = await CreateRepository().ListDueConnectionsAsync(_now);

        connections.Should().NotContain(stored => stored.Id == connection.Id);
    }

    [Test]
    public async Task PostponeDueEntriesAsync_DueEntries_CountsTheAttemptAndMovesThem()
    {
        User user = await AddUserAsync();
        ScrobbleConnection connection = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.ListenBrainz, "listener"));
        ScrobbleQueueEntry entry = Entry(connection, _now.AddMinutes(-5));
        await CreateRepository().EnqueueAsync([entry]);

        await CreateRepository().PostponeDueEntriesAsync(connection.Id, _now, _now.AddMinutes(2));

        await using WaveMpDbContext dbContext = CreateDbContext();
        ScrobbleQueueEntry stored = await dbContext.ScrobbleQueue.SingleAsync(queued => queued.Id == entry.Id);
        stored.Attempts.Should().Be(1);
        stored.NextAttemptAt.Should().Be(_now.AddMinutes(2));
    }

    [Test]
    public async Task RemoveEntriesAsync_WithTime_RemovesThemAndNotesTheScrobble()
    {
        User user = await AddUserAsync();
        ScrobbleConnection connection = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.ListenBrainz, "listener"));
        ScrobbleQueueEntry sent = Entry(connection, _now.AddMinutes(-5));
        ScrobbleQueueEntry kept = Entry(connection, _now.AddMinutes(-4));
        await CreateRepository().EnqueueAsync([sent, kept]);

        await CreateRepository().RemoveEntriesAsync(connection.Id, [sent.Id], _now);

        (await CreateRepository().CountQueuedAsync(user.Id)).Should().Equal(
            new Dictionary<Guid, int> { [connection.Id] = 1 });
        (await CreateRepository().ListConnectionsAsync(user.Id)).Single().LastScrobbledAt.Should().Be(_now);
    }

    [Test]
    public async Task RemoveExpiredAsync_OldEntries_RemovesOnlyThem()
    {
        User user = await AddUserAsync();
        ScrobbleConnection connection = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.ListenBrainz, "listener"));
        await CreateRepository().EnqueueAsync([Entry(connection, _now.AddDays(-15)), Entry(connection, _now)]);

        int removed = await CreateRepository().RemoveExpiredAsync(_now.AddDays(-14));

        removed.Should().BeGreaterThanOrEqualTo(1);
        (await CreateRepository().CountQueuedAsync(user.Id)).Should().Equal(
            new Dictionary<Guid, int> { [connection.Id] = 1 });
    }

    [Test]
    public async Task NextAttemptAtAsync_PostponedEntry_ReturnsTheEarliestOfActiveConnections()
    {
        User user = await AddUserAsync();
        ScrobbleConnection connection = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.ListenBrainz, "listener"));
        ScrobbleQueueEntry entry = Entry(connection, _now.AddMinutes(-5));
        entry.NextAttemptAt = _now.AddYears(5);
        await CreateRepository().EnqueueAsync([entry]);

        DateTimeOffset? next = await CreateRepository().NextAttemptAtAsync();

        next.Should().NotBeNull();
        next!.Value.Should().BeOnOrBefore(_now.AddYears(5));
    }

    [Test]
    public async Task DeleteUser_WithConnection_RemovesTheConnectionAndQueue()
    {
        User user = await AddUserAsync();
        ScrobbleConnection connection = await CreateRepository().SaveConnectionAsync(
            Connection(user, ScrobbleService.ListenBrainz, "listener"));
        await CreateRepository().EnqueueAsync([Entry(connection, _now)]);

        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            await dbContext.Users.Where(stored => stored.Id == user.Id).ExecuteDeleteAsync();
        }

        await using WaveMpDbContext check = CreateDbContext();
        (await check.ScrobbleConnections.AnyAsync(stored => stored.Id == connection.Id)).Should().BeFalse();
        (await check.ScrobbleQueue.AnyAsync(entry => entry.ConnectionId == connection.Id)).Should().BeFalse();
    }

    private ScrobbleRepository CreateRepository()
    {
        return new ScrobbleRepository(CreateDbContext());
    }

    private static ScrobbleConnection Connection(User user, ScrobbleService service, string account)
    {
        return new ScrobbleConnection
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Service = service,
            AccountName = account,
            ProtectedCredential = "protected",
            Status = ScrobbleConnectionStatus.Active,
            ConnectedAt = _now,
        };
    }

    private static ScrobbleQueueEntry Entry(ScrobbleConnection connection, DateTimeOffset playedAt)
    {
        return new ScrobbleQueueEntry
        {
            Id = Guid.CreateVersion7(),
            ConnectionId = connection.Id,
            Artist = "Artist",
            Title = "Title",
            Duration = TimeSpan.FromMinutes(3),
            PlayedAt = playedAt,
            NextAttemptAt = playedAt,
        };
    }

    private async Task<User> AddUserAsync()
    {
        string name = "Listener " + Guid.NewGuid().ToString("N")[..8];
        User user = new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Initials = "L",
            Color = "#336699",
            Role = UserRole.User,
            IsBuiltIn = false,
            CreatedAt = _now,
        };
        await using WaveMpDbContext dbContext = CreateDbContext();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}
