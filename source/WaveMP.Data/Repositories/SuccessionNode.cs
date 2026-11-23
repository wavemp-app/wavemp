namespace WaveMP.Data.Repositories;

/// <summary>
/// An album or artist as <see cref="SuccessionPlanner"/> sees it: whether it still has songs (one without is left
/// empty and deleted) and its first seen date.
/// </summary>
internal sealed record SuccessionNode(Guid Id, bool HasSongs, DateTimeOffset FirstSeenAt);
