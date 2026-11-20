using WaveMP.Core.Recaps.Statistics;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Recaps.Shares;

internal sealed class RecapShareService(
    IRecapShareRepository shares,
    IListeningStatisticsService statistics,
    TimeProvider timeProvider) : IRecapShareService
{
    public Task<Page<RecapShareRow>> ListAsync(
        Guid userId,
        bool own,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return shares.ListAsync(new RecapShareListQuery(userId, own, offset, limit), cancellationToken);
    }

    public Task<RecapShareRow?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return shares.FindAsync(id, cancellationToken);
    }

    public Task<RecapShareSaveResult> ShareAsync(
        RecapShareRequest request,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        RecapShare share = new()
        {
            Id = Guid.CreateVersion7(now),
            UserId = request.UserId,
            Period = request.Period,
            FromDate = request.From,
            ToDate = request.To,
            TimeZone = request.TimeZone,
            Design = request.Design,
            CreatedAt = TruncateToMicroseconds(now),
            UpdatedAt = TruncateToMicroseconds(now),
        };
        return shares.SaveAsync(share, cancellationToken);
    }

    public async Task<RecapShareDeletion> DeleteAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        RecapShareRow? row = await shares.FindAsync(id, cancellationToken);
        if (row is null)
        {
            return RecapShareDeletion.NotFound;
        }

        if (row.Share.UserId != userId)
        {
            return RecapShareDeletion.NotOwner;
        }

        return await shares.DeleteAsync(userId, id, cancellationToken)
            ? RecapShareDeletion.Deleted
            : RecapShareDeletion.NotFound;
    }

    public async Task<SharedRecap?> GetStatisticsAsync(
        Guid id,
        Guid viewerId,
        int top,
        CancellationToken cancellationToken = default)
    {
        RecapShareRow? row = await shares.FindAsync(id, cancellationToken);
        if (row is null || !StatisticsTimeZones.TryFind(row.Share.TimeZone, out TimeZoneInfo timeZone))
        {
            return null;
        }

        ListeningStatistics result = await statistics.GetAsync(
            new StatisticsQuery(row.Share.UserId, row.Share.FromDate, row.Share.ToDate, timeZone, top, viewerId),
            cancellationToken);
        return new SharedRecap(row, result);
    }

    /// <summary>
    /// PostgreSQL keeps microseconds; the new share is returned with the precision it is read with later.
    /// </summary>
    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        return new DateTimeOffset(value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond), value.Offset);
    }
}
