using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// What an album shows without a cover of its own, as stored, and its album artist, whose picture it may show.
/// </summary>
/// <param name="Fallback">The exception of the album; <c>null</c> inherits the global rule of the library settings.
/// </param>
/// <param name="AlbumArtistId">The album artist; <c>null</c> for albums without one.</param>
public sealed record AlbumCoverFallbackRow(AlbumCoverFallback? Fallback, Guid? AlbumArtistId);
