namespace WaveMP.Data.Repositories;

/// <summary>
/// An online cover that changes its album.
/// </summary>
internal sealed record AlbumSuccessionOnlineCover(Guid OnlineCoverId, Guid AlbumId);
