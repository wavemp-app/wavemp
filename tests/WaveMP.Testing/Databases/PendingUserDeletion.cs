using Npgsql;

namespace WaveMP.Testing.Databases;

/// <summary>
/// Deletes a user in a transaction of its own that stays open until <see cref="CommitWhileWaitingAsync"/>: until then
/// the row of the user stays locked, while other connections still see the user. A write for the user that starts in
/// this time has to wait for the deletion, like a write of a request whose profile is deleted while it runs.
/// </summary>
public sealed class PendingUserDeletion : IAsyncDisposable
{
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(20);

    private readonly string _connectionString;
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;

    private PendingUserDeletion(string connectionString, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connectionString = connectionString;
        _connection = connection;
        _transaction = transaction;
    }

    /// <summary>
    /// Deletes the user (with its settings and sessions) without committing the deletion yet.
    /// </summary>
    public static async Task<PendingUserDeletion> StartAsync(string connectionString, Guid userId)
    {
        NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await using NpgsqlCommand command = new("DELETE FROM users WHERE id = @id", connection, transaction);
        command.Parameters.AddWithValue("id", userId);
        int deleted = await command.ExecuteNonQueryAsync();
        if (deleted != 1)
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw new InvalidOperationException($"The user {userId} does not exist.");
        }

        return new PendingUserDeletion(connectionString, connection, transaction);
    }

    /// <summary>
    /// Waits until another connection to the database waits for a lock (the write for the user) or
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
