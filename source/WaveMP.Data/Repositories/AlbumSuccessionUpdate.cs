using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// New values of an album that took over the data of albums left empty.
/// </summary>
internal sealed record AlbumSuccessionUpdate(
    Guid AlbumId,
    DateTimeOffset FirstSeenAt,
    CoverPriority? CoverPriority,
    AlbumCoverFallback? CoverFallback);
