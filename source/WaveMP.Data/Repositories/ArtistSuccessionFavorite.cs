namespace WaveMP.Data.Repositories;

/// <summary>
/// An artist favorite of a user.
/// </summary>
internal sealed record ArtistSuccessionFavorite(Guid UserId, Guid ArtistId, DateTimeOffset CreatedAt);
