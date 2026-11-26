using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// An album as the carry-over of <see cref="AlbumSuccessionPlanner"/> sees it: whether it still has tracks (an album
/// without is left empty and deleted), its first seen date, its cover rules and its online cover, if any.
/// </summary>
internal sealed record AlbumSuccessionAlbum(
    Guid Id,
    bool HasTracks,
    DateTimeOffset FirstSeenAt,
    CoverPriority? CoverPriority,
    AlbumCoverFallback? CoverFallback,
    Guid? OnlineCoverId);
