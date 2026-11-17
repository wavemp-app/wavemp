using Microsoft.Extensions.Logging;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Reviews;

internal sealed class TrackReviewService(
    ITrackReviewRepository reviews,
    TimeProvider timeProvider,
    ILogger<TrackReviewService> logger) : ITrackReviewService
{
    public Task<Page<TrackReviewRow>> ListAsync(
        TrackReviewListRequest request,
        CancellationToken cancellationToken = default)
    {
        TrackReviewSortField sort = request.Sort ?? TrackReviewSortField.FirstSeenAt;
        SortDirection direction = request.Direction
            ?? (sort == TrackReviewSortField.FirstSeenAt ? SortDirection.Desc : SortDirection.Asc);
        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        return reviews.ListAsync(
            new TrackReviewListQuery(
                request.Offset,
                request.Limit,
                sort,
                direction,
                request.Status ?? TrackReviewStatus.Unreviewed,
                request.MissingTags,
                search),
            cancellationToken);
    }

    public Task<TrackReviewCounts> CountAsync(CancellationToken cancellationToken = default)
    {
        return reviews.CountAsync(cancellationToken);
    }

    public Task<TrackReviewStateRow?> GetAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        return reviews.FindAsync(trackId, cancellationToken);
    }

    public async Task<TrackReviewStateRow?> SetAsync(
        Guid trackId,
        TrackReviewStatus status,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await SetManyAsync([trackId], status, userId, cancellationToken);
        return await reviews.FindAsync(trackId, cancellationToken);
    }

    public async Task<int> SetManyAsync(
        IReadOnlyCollection<Guid> trackIds,
        TrackReviewStatus status,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        Guid[] ids = [.. trackIds.Distinct()];
        if (ids.Length > TrackReviews.MaxBatchSize)
        {
            throw new ArgumentException(
                $"At most {TrackReviews.MaxBatchSize} tracks can be marked at once.",
                nameof(trackIds));
        }

        if (ids.Length == 0)
        {
            return 0;
        }

        int changed = status == TrackReviewStatus.Reviewed
            ? await reviews.MarkReviewedAsync(ids, userId, timeProvider.GetUtcNow(), cancellationToken)
            : await reviews.MarkUnreviewedAsync(ids, cancellationToken);
        if (changed > 0)
        {
            logger.LogInformation("{Count} tracks marked as {Status} by {UserId}", changed, status, userId);
        }

        return changed;
    }
}
