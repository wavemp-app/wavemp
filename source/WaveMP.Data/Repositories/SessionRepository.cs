using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;

namespace WaveMP.Data.Repositories;

internal sealed class SessionRepository(WaveMpDbContext dbContext) : ISessionRepository
{
    public Task AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        return dbContext.InUserTransactionAsync(
            session.UserId,
            async token =>
            {
                dbContext.Sessions.Add(session);
                return await dbContext.SaveChangesAsync(token);
            },
            cancellationToken);
    }

    public Task<Session?> FindActiveAsync(
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Sessions.AsNoTracking()
            .Include(session => session.User)
            .FirstOrDefaultAsync(session => session.Id == sessionId && session.ExpiresAt > now, cancellationToken);
    }

    public Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return dbContext.Sessions
            .Where(session => session.Id == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteForUserAsync(
        Guid userId,
        Guid? exceptSessionId = null,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Sessions
            .Where(session => session.UserId == userId && session.Id != exceptSessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return dbContext.Sessions
            .Where(session => session.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
