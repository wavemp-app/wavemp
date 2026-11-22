namespace WaveMP.Data.Repositories;

/// <summary>
/// An album favorite of a user.
/// </summary>
internal sealed record AlbumSuccessionFavorite(Guid UserId, Guid AlbumId, DateTimeOffset CreatedAt);
