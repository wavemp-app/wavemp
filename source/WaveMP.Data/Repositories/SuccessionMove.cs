namespace WaveMP.Data.Repositories;

/// <summary>
/// Songs that moved from one album or artist to another one, as <see cref="SuccessionPlanner"/> sees it.
/// </summary>
internal sealed record SuccessionMove(Guid FromId, Guid ToId, int TrackCount, DateTimeOffset MovedAt);
