using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;

namespace WaveMP.Data.Repositories;

internal sealed class ScrobbleRepository(WaveMpDbContext dbContext) : IScrobbleRepository
{
    public async Task<IReadOnlyList<ScrobbleConnection>> ListConnectionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ScrobbleConnections.AsNoTracking()
            .Where(connection => connection.UserId == userId)
            .OrderBy(connection => connection.Service)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountQueuedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ScrobbleQueue.AsNoTracking()
            .Where(entry => entry.Connection!.UserId == userId)
            .GroupBy(entry => entry.ConnectionId)
            .Select(group => new { ConnectionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.ConnectionId, group => group.Count, cancellationToken);
    }

    public Task<ScrobbleConnection> SaveConnectionAsync(
        ScrobbleConnection connection,
        CancellationToken cancellationToken = default)
    {
        return dbContext.InUserTransactionAsync(
            connection.UserId,
            async token =>
            {
                // The lock of the user also serializes two connections to the same service at the same time.
                ScrobbleConnection? existing = await dbContext.ScrobbleConnections
                    .FirstOrDefaultAsync(
                        stored => stored.UserId == connection.UserId && stored.Service == connection.Service,
                        token);
                ScrobbleConnection saved;
                if (existing is null)
                {
                    saved = new ScrobbleConnection
                    {
                        Id = connection.Id == Guid.Empty ? Guid.CreateVersion7() : connection.Id,
                        UserId = connection.UserId,
                        Service = connection.Service,
                        AccountName = connection.AccountName,
                        ProtectedCredential = connection.ProtectedCredential,
                        Status = connection.Status,
                        ConnectedAt = connection.ConnectedAt,
                        LastScrobbledAt = connection.LastScrobbledAt,
                    };
                    dbContext.ScrobbleConnections.Add(saved);
                }
                else
                {
                    existing.AccountName = connection.AccountName;
                    existing.ProtectedCredential = connection.ProtectedCredential;
                    existing.Status = connection.Status;
                    existing.ConnectedAt = connection.ConnectedAt;
                    saved = existing;
                }

                try
                {
                    await dbContext.SaveChangesAsync(token);
                }
                finally
                {
                    dbContext.Entry(saved).State = EntityState.Detached;
                }

                return saved;
            },
            cancellationToken);
    }

    public async Task<bool> DeleteConnectionAsync(
        Guid userId,
        ScrobbleService service,
        CancellationToken cancellationToken = default)
    {
        // The database removes the queued scrobbles of the connection (cascade).
        int deleted = await dbContext.ScrobbleConnections
            .Where(connection => connection.UserId == userId && connection.Service == service)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public Task SetConnectionStatusAsync(
        Guid connectionId,
        ScrobbleConnectionStatus status,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ScrobbleConnections
            .Where(connection => connection.Id == connectionId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(connection => connection.Status, status),
                cancellationToken);
    }

    public async Task<int> EnqueueAsync(
        IReadOnlyCollection<ScrobbleQueueEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return 0;
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                Guid[] connectionIds = [.. entries.Select(entry => entry.ConnectionId).Distinct()];

                // FOR KEY SHARE like the check of the foreign key: a deletion of a connection waits for the commit.
                List<Guid> locked = await dbContext.Database
                    .SqlQuery<Guid>(
                        $"""
                        SELECT id AS "Value" FROM scrobble_connections
                        WHERE id = ANY({connectionIds}) FOR KEY SHARE
                        """)
                    .ToListAsync(token);
                HashSet<Guid> existing = [.. locked];
                ScrobbleQueueEntry[] added = [.. entries.Where(entry => existing.Contains(entry.ConnectionId))];
                dbContext.ScrobbleQueue.AddRange(added);
                try
                {
                    await dbContext.SaveChangesAsync(token);
                }
                finally
                {
                    foreach (ScrobbleQueueEntry entry in added)
                    {
                        dbContext.Entry(entry).State = EntityState.Detached;
                    }
                }

                await transaction.CommitAsync(token);
                return added.Length;
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<ScrobbleConnection>> ListDueConnectionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ScrobbleConnections.AsNoTracking()
            .Where(connection => connection.Status == ScrobbleConnectionStatus.Active
                && dbContext.ScrobbleQueue.Any(entry =>
                    entry.ConnectionId == connection.Id && entry.NextAttemptAt <= now))
            .OrderBy(connection => connection.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScrobbleQueueEntry>> ListDueEntriesAsync(
        Guid connectionId,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ScrobbleQueue.AsNoTracking()
            .Where(entry => entry.ConnectionId == connectionId && entry.NextAttemptAt <= now)
            .OrderBy(entry => entry.PlayedAt)
            .ThenBy(entry => entry.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveEntriesAsync(
        Guid connectionId,
        IReadOnlyCollection<Guid> entryIds,
        DateTimeOffset? scrobbledAt,
        CancellationToken cancellationToken = default)
    {
        if (entryIds.Count > 0)
        {
            Guid[] ids = [.. entryIds];
            await dbContext.ScrobbleQueue
                .Where(entry => entry.ConnectionId == connectionId && ids.Contains(entry.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (scrobbledAt is { } at)
        {
            await dbContext.ScrobbleConnections
                .Where(connection => connection.Id == connectionId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(connection => connection.LastScrobbledAt, at),
                    cancellationToken);
        }
    }

    public Task PostponeDueEntriesAsync(
        Guid connectionId,
        DateTimeOffset now,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ScrobbleQueue
            .Where(entry => entry.ConnectionId == connectionId && entry.NextAttemptAt <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.Attempts, entry => entry.Attempts + 1)
                    .SetProperty(entry => entry.NextAttemptAt, nextAttemptAt),
                cancellationToken);
    }

    public Task<int> RemoveExpiredAsync(DateTimeOffset playedBefore, CancellationToken cancellationToken = default)
    {
        return dbContext.ScrobbleQueue
            .Where(entry => entry.PlayedAt < playedBefore)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> NextAttemptAtAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.ScrobbleQueue.AsNoTracking()
            .Where(entry => entry.Connection!.Status == ScrobbleConnectionStatus.Active)
            .MinAsync(entry => (DateTimeOffset?)entry.NextAttemptAt, cancellationToken);
    }
}
