using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The rules of a smart playlist as the upgrade to normalized names sees them (see
/// <see cref="INameKeyUpgradeRepository"/>).
/// </summary>
public sealed record NameKeyUpgradeSmartPlaylist(Guid Id, IReadOnlyList<SmartPlaylistRule> Rules);
