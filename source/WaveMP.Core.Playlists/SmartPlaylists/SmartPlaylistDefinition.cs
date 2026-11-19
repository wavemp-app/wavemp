using WaveMP.Data.Entities;
using WaveMP.Data.Queries;

namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// A smart playlist as a user enters it, before it is checked (see <see cref="SmartPlaylistRules.Validate"/>).
/// </summary>
/// <param name="Name">Name; the rules of playlist names apply.</param>
/// <param name="Match">Whether a track has to meet all rules or at least one.</param>
/// <param name="Rules">The rules in their order, at most <see cref="SmartPlaylistRules.MaxRules"/>.</param>
/// <param name="Sort">Sort key of the tracks.</param>
/// <param name="Direction">Sort direction of the key.</param>
/// <param name="Limit">Largest number of tracks, 1 to <see cref="SmartPlaylistRules.MaxLimit"/>; <c>null</c> for all.
/// </param>
public sealed record SmartPlaylistDefinition(
    string? Name,
    SmartPlaylistMatch Match,
    IReadOnlyList<SmartPlaylistRule> Rules,
    SmartPlaylistSortField Sort,
    SortDirection Direction,
    int? Limit = null);
