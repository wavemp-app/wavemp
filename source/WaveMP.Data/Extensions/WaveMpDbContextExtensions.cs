using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WaveMP.Data.Repositories;

namespace WaveMP.Data.Extensions;

/// <summary>
/// Writes of data that belongs to a user or refers to the library, safe against a concurrent deletion of the user or
/// of the referenced row. A row is locked <c>FOR KEY SHARE</c> (the lock that a foreign key check takes as well): a
/// deletion of the row waits until the transaction of the write ends and then also removes the new data (cascade),
/// while a row deleted before is reported instead of failing the write with a foreign key violation.
/// </summary>
internal static class WaveMpDbContextExtensions
{
    /// <summary>
    /// Runs the work in a transaction that first locks the row of the user (see <see cref="LockUserAsync"/>) and is
    /// committed after the work. The execution strategy may repeat the whole work after a transient error.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user does not exist; the work does not run.</exception>
    public static Task<T> InUserTransactionAsync<T>(
        this WaveMpDbContext dbContext,
        Guid userId,
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await dbContext.LockUserAsync(userId, token);
                T result = await work(token);
                await transaction.CommitAsync(token);
                return result;
            },
            cancellationToken);
    }

    /// <summary>
    /// Locks the row of the user until the end of the current transaction; a deletion of the user waits until then.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user does not exist.</exception>
    public static async Task LockUserAsync(
        this WaveMpDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.LockAsync(
            $"""SELECT 1 AS "Value" FROM users WHERE id = {userId} FOR KEY SHARE""",
            cancellationToken))
        {
            throw new UserNotFoundException(userId);
        }
    }

    /// <summary>
    /// Locks the row of the track until the end of the current transaction. Returns <c>false</c> if the track does not
    /// exist.
    /// </summary>
    public static Task<bool> LockTrackAsync(
        this WaveMpDbContext dbContext,
        Guid trackId,
        CancellationToken cancellationToken)
    {
        return dbContext.LockAsync(
            $"""SELECT 1 AS "Value" FROM tracks WHERE id = {trackId} FOR KEY SHARE""",
            cancellationToken);
    }

    /// <summary>
    /// Locks the row of the album until the end of the current transaction. Returns <c>false</c> if the album does not
    /// exist.
    /// </summary>
    public static Task<bool> LockAlbumAsync(
        this WaveMpDbContext dbContext,
        Guid albumId,
        CancellationToken cancellationToken)
    {
        return dbContext.LockAsync(
            $"""SELECT 1 AS "Value" FROM albums WHERE id = {albumId} FOR KEY SHARE""",
            cancellationToken);
    }

    /// <summary>
    /// Locks the row of the artist until the end of the current transaction. Returns <c>false</c> if the artist does
    /// not exist.
    /// </summary>
    public static Task<bool> LockArtistAsync(
        this WaveMpDbContext dbContext,
        Guid artistId,
        CancellationToken cancellationToken)
    {
        return dbContext.LockAsync(
            $"""SELECT 1 AS "Value" FROM artists WHERE id = {artistId} FOR KEY SHARE""",
            cancellationToken);
    }

    /// <summary>
    /// Runs a locking query of at most one row as it is (not composed) and returns whether it found the row.
    /// </summary>
    private static async Task<bool> LockAsync(
        this WaveMpDbContext dbContext,
        FormattableString sql,
        CancellationToken cancellationToken)
    {
        List<int> rows = await dbContext.Database.SqlQuery<int>(sql).ToListAsync(cancellationToken);
        return rows.Count > 0;
    }
}
