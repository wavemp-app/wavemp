using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scrobbling.Tests.Queues;

/// <summary>
/// The scrobbling tables in memory, with the behavior the queue relies on (the repository itself is tested against
/// PostgreSQL in WaveMP.Data.Tests).
/// </summary>
internal sealed class InMemoryScrobbleRepository : IScrobbleRepository
{
    private static readonly DateTimeOffset _created = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    public Guid UserId { get; } = Guid.NewGuid();

    public List<ScrobbleConnection> Connections { get; } = [];

    public List<ScrobbleQueueEntry> Entries { get; } = [];

    public ScrobbleConnection AddConnection(ScrobbleService service, string protectedCredential)
    {
        ScrobbleConnection connection = new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Service = service,
            AccountName = "listener",
            ProtectedCredential = protectedCredential,
            Status = ScrobbleConnectionStatus.Active,
            ConnectedAt = _created,
        };
        Connections.Add(connection);
        return connection;
    }

    public ScrobbleQueueEntry AddEntry(ScrobbleConnection connection, string title, DateTimeOffset playedAt)
    {
        ScrobbleQueueEntry entry = new()
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            Artist = "Artist",
            Title = title,
            PlayedAt = playedAt,
            NextAttemptAt = playedAt,
        };
        Entries.Add(entry);
        return entry;
    }

    public Task<IReadOnlyList<ScrobbleConnection>> ListConnectionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ScrobbleConnection>>(
            [.. Connections.Where(connection => connection.UserId == userId)]);
    }

    public Task<IReadOnlyDictionary<Guid, int>> CountQueuedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(Entries
            .GroupBy(entry => entry.ConnectionId)
            .ToDictionary(group => group.Key, group => group.Count()));
    }

    public Task<ScrobbleConnection> SaveConnectionAsync(
        ScrobbleConnection connection,
        CancellationToken cancellationToken = default)
    {
        Connections.RemoveAll(stored => stored.UserId == connection.UserId && stored.Service == connection.Service);
        Connections.Add(connection);
        return Task.FromResult(connection);
    }

    public Task<bool> DeleteConnectionAsync(
        Guid userId,
        ScrobbleService service,
        CancellationToken cancellationToken = default)
    {
        ScrobbleConnection? connection = Connections.FirstOrDefault(stored =>
            stored.UserId == userId && stored.Service == service);
        if (connection is null)
        {
            return Task.FromResult(false);
        }

        Connections.Remove(connection);
        Entries.RemoveAll(entry => entry.ConnectionId == connection.Id);
        return Task.FromResult(true);
    }

    public Task SetConnectionStatusAsync(
        Guid connectionId,
        ScrobbleConnectionStatus status,
        CancellationToken cancellationToken = default)
    {
        foreach (ScrobbleConnection connection in Connections.Where(connection => connection.Id == connectionId))
        {
            connection.Status = status;
        }

        return Task.CompletedTask;
    }

    public Task<int> EnqueueAsync(
        IReadOnlyCollection<ScrobbleQueueEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ScrobbleQueueEntry[] added =
            [.. entries.Where(entry => Connections.Any(connection => connection.Id == entry.ConnectionId))];
        Entries.AddRange(added);
        return Task.FromResult(added.Length);
    }

    public Task<IReadOnlyList<ScrobbleConnection>> ListDueConnectionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ScrobbleConnection>>([.. Connections.Where(connection =>
            connection.Status == ScrobbleConnectionStatus.Active
            && Entries.Any(entry => entry.ConnectionId == connection.Id && entry.NextAttemptAt <= now))]);
    }

    public Task<IReadOnlyList<ScrobbleQueueEntry>> ListDueEntriesAsync(
        Guid connectionId,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ScrobbleQueueEntry>>([.. Entries
            .Where(entry => entry.ConnectionId == connectionId && entry.NextAttemptAt <= now)
            .OrderBy(entry => entry.PlayedAt)
            .Take(limit)]);
    }

    public Task RemoveEntriesAsync(
        Guid connectionId,
        IReadOnlyCollection<Guid> entryIds,
        DateTimeOffset? scrobbledAt,
        CancellationToken cancellationToken = default)
    {
        Entries.RemoveAll(entry => entry.ConnectionId == connectionId && entryIds.Contains(entry.Id));
        if (scrobbledAt is { } at)
        {
            Connections.Single(connection => connection.Id == connectionId).LastScrobbledAt = at;
        }

        return Task.CompletedTask;
    }

    public Task PostponeDueEntriesAsync(
        Guid connectionId,
        DateTimeOffset now,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default)
    {
        foreach (ScrobbleQueueEntry entry in Entries.Where(entry =>
            entry.ConnectionId == connectionId && entry.NextAttemptAt <= now))
        {
            entry.Attempts++;
            entry.NextAttemptAt = nextAttemptAt;
        }

        return Task.CompletedTask;
    }

    public Task<int> RemoveExpiredAsync(DateTimeOffset playedBefore, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Entries.RemoveAll(entry => entry.PlayedAt < playedBefore));
    }

    public Task<DateTimeOffset?> NextAttemptAtAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset[] due = [.. Entries
            .Where(entry => Connections.Any(connection =>
                connection.Id == entry.ConnectionId && connection.Status == ScrobbleConnectionStatus.Active))
            .Select(entry => entry.NextAttemptAt)];
        return Task.FromResult<DateTimeOffset?>(due.Length == 0 ? null : due.Min());
    }
}
