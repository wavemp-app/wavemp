using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

internal sealed class RecapShareRepository(WaveMpDbContext dbContext) : IRecapShareRepository
{
    public Task<Page<RecapShareRow>> ListAsync(
        RecapShareListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<RecapShare> shares = dbContext.RecapShares.AsNoTracking();
        shares = query.Own
            ? shares.Where(share => share.UserId == query.UserId)
            : shares.Where(share => share.UserId != query.UserId);

        return shares
            .OrderByDescending(share => share.UpdatedAt)
            .ThenByDescending(share => share.Id)
            .Select(share => new RecapShareRow(share, share.User!.Name))
            .ToPageAsync(query.Offset, query.Limit, cancellationToken);
    }

    public Task<RecapShareRow?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.RecapShares.AsNoTracking()
            .Where(share => share.Id == id)
            .Select(share => new RecapShareRow(share, share.User!.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<RecapShareSaveResult> SaveAsync(RecapShare share, CancellationToken cancellationToken = default)
    {
        return dbContext.InUserTransactionAsync(
            share.UserId,
            async token =>
            {
                // The lock of the user serializes the shares of one user, so two requests cannot both create one.
                dbContext.ChangeTracker.Clear();
                RecapShare? existing = await dbContext.RecapShares
                    .FirstOrDefaultAsync(
                        candidate => candidate.UserId == share.UserId
                            && candidate.Period == share.Period
                            && candidate.FromDate == share.FromDate
                            && candidate.ToDate == share.ToDate
                            && candidate.TimeZone == share.TimeZone,
                        token);
                bool created = existing is null;
                if (existing is null)
                {
                    dbContext.RecapShares.Add(share);
                    existing = share;
                }
                else
                {
                    existing.Design = share.Design;
                    existing.UpdatedAt = share.UpdatedAt;
                }

                await dbContext.SaveChangesAsync(token);
                dbContext.ChangeTracker.Clear();
                string ownerName = await dbContext.Users.AsNoTracking()
                    .Where(user => user.Id == share.UserId)
                    .Select(user => user.Name)
                    .FirstAsync(token);
                return new RecapShareSaveResult(new RecapShareRow(existing, ownerName), created);
            },
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        int deleted = await dbContext.RecapShares
            .Where(share => share.Id == id && share.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }
}
