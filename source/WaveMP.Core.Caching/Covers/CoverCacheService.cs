using WaveMP.Core.Library.Covers;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Caching.Covers;

internal sealed class CoverCacheService(ICoverService covers, ICoverImageCache cache) : ICoverCacheService
{
    public async Task<CoverDeliveryResult> GetTrackCoverAsync(
        Guid trackId,
        int? size,
        CancellationToken cancellationToken = default)
    {
        CoverCandidates candidates = await covers.GetTrackCandidatesAsync(trackId, cancellationToken);
        return await DeliverAsync(candidates, size, cancellationToken);
    }

    public async Task<CoverDeliveryResult> GetAlbumCoverAsync(
        Guid albumId,
        int? size,
        CancellationToken cancellationToken = default)
    {
        CoverCandidates candidates = await covers.GetAlbumCandidatesAsync(albumId, cancellationToken);
        return await DeliverAsync(candidates, size, cancellationToken);
    }

    public async Task<CoverDeliveryResult> PreviewTrackCoverAsync(
        Guid trackId,
        CoverPriority? rule,
        int? size,
        CancellationToken cancellationToken = default)
    {
        CoverCandidates candidates = await covers.PreviewTrackCandidatesAsync(trackId, rule, cancellationToken);
        return await DeliverAsync(candidates, size, cancellationToken);
    }

    public async Task<CoverDeliveryResult> PreviewAlbumCoverAsync(
        Guid albumId,
        CoverPriority? rule,
        int? size,
        CancellationToken cancellationToken = default)
    {
        CoverCandidates candidates = await covers.PreviewAlbumCandidatesAsync(albumId, rule, cancellationToken);
        return await DeliverAsync(candidates, size, cancellationToken);
    }

    private async Task<CoverDeliveryResult> DeliverAsync(
        CoverCandidates candidates,
        int? size,
        CancellationToken cancellationToken)
    {
        if (!candidates.OwnerExists)
        {
            return CoverDeliveryResult.OwnerNotFound;
        }

        CoverDelivery? delivery = await cache.DeliverAsync(candidates.Ordered, size, cancellationToken);
        return delivery is null ? CoverDeliveryResult.NotFound : CoverDeliveryResult.Found(delivery);
    }
}
