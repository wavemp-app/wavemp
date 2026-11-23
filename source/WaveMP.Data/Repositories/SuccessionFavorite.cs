namespace WaveMP.Data.Repositories;

/// <summary>
/// A favorite of a user for an album or artist, as <see cref="SuccessionPlanner"/> sees it.
/// </summary>
internal sealed record SuccessionFavorite(Guid UserId, Guid TargetId, DateTimeOffset CreatedAt);
