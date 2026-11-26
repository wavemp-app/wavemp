namespace WaveMP.Data.Repositories;

/// <summary>
/// An artist as the upgrade to normalized names sees it (see <see cref="INameKeyUpgradeRepository"/>).
/// </summary>
public sealed record NameKeyUpgradeArtist(Guid Id, string Name, DateTimeOffset FirstSeenAt);
