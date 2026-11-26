namespace WaveMP.Data.Repositories;

/// <summary>
/// A genre as the upgrade to normalized names sees it (see <see cref="INameKeyUpgradeRepository"/>), with the number
/// of its tracks.
/// </summary>
public sealed record NameKeyUpgradeGenre(Guid Id, string Name, int TrackCount);
