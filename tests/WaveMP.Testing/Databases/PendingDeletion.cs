using Npgsql;

namespace WaveMP.Testing.Databases;

/// <summary>
/// Deletes a row in a transaction of its own that stays open until <see cref="CommitWhileWaitingAsync"/>: until then
/// the row stays locked, while other connections still see it. A write that refers to the row and starts in this time
/// has to wait for the deletion, like a write of a request whose profile is deleted while it runs, or one that refers
/// to a track a scan removes meanwhile.
/// </summary>
public sealed class PendingDeletion : IAsyncDisposable
{
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(20);

    private readonly string _connectionString;
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;

    private PendingDeletion(string connectionString, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connectionString = connectionString;
        _connection = connection;
        _transaction = transaction;
    }

    /// <summary>
    /// Deletes the user (with its settings and sessions) without committing the deletion yet.
    /// </summary>
    public static Task<PendingDeletion> OfUserAsync(string connectionString, Guid userId)
    {
        return StartAsync(connectionString, "DELETE FROM users WHERE id = @id", "user", userId);
    }

    /// <summary>
    /// Deletes the track (with its files, covers and the entries of playlists), as a scan removes a track whose files
    /// are gone, without committing the deletion yet.
    /// </summary>
    public static Task<PendingDeletion> OfTrackAsync(string connectionString, Guid trackId)
    {
        return StartAsync(connectionString, "DELETE FROM tracks WHERE id = @id", "track", trackId);
    }

    /// <summary>
    /// Waits until another connection to the database waits for a lock (the write that refers to the row) or
    /// <paramref name="write"/> has ended, and commits the deletion then. Returns whether the write waited for the
    /// deletion.
    /// </summary>
    public async Task<bool> CommitWhileWaitingAsync(Task write, TimeSpan timeout)
    {
        bool waited = await WaitForLockAsync(write, timeout);
        await _transaction.CommitAsync();
        return waited;
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static async Task<PendingDeletion> StartAsync(
        string connectionString,
        string deletion,
        string kind,
        Guid id)
    {
        NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await using NpgsqlCommand command = new(deletion, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        int deleted = await command.ExecuteNonQueryAsync();
        if (deleted != 1)
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw new InvalidOperationException($"The {kind} {id} does not exist.");
        }

        return new PendingDeletion(connectionString, connection, transaction);
    }

    private async Task<bool> WaitForLockAsync(Task write, TimeSpan timeout)
    {
        // A connection of its own: statistics read inside a transaction keep their first values.
        await using NpgsqlConnection observer = new(_connectionString);
        await observer.OpenAsync();
        await using NpgsqlCommand command = new(
            """
            SELECT count(*) FROM pg_stat_activity
            WHERE datname = current_database() AND wait_event_type = 'Lock' AND pid <> pg_backend_pid()
            """,
            observer);

        DateTime end = DateTime.UtcNow + timeout;
        while (!write.IsCompleted && DateTime.UtcNow < end)
        {
            if ((long)(await command.ExecuteScalarAsync() ?? 0L) > 0)
            {
                return true;
            }

            await Task.Delay(_pollInterval);
        }

        return false;
    }
}
