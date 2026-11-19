using WaveMP.Data.Entities;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// What a smart playlist picks from the library and in which order: its rules, how they are combined, the sort order
/// and the optional limit.
/// </summary>
/// <param name="Match">Whether a track has to meet all rules or at least one.</param>
/// <param name="Rules">The rules; without rules every track matches.</param>
/// <param name="Sort">Sort key of the tracks.</param>
/// <param name="Direction">Sort direction of the key.</param>
/// <param name="Limit">Largest number of tracks, the first ones in the sort order; <c>null</c> for all.</param>
public sealed record SmartPlaylistCriteria(
    SmartPlaylistMatch Match,
    IReadOnlyList<SmartPlaylistRule> Rules,
    SmartPlaylistSortField Sort,
    SortDirection Direction,
    int? Limit = null)
{
    /// <summary>
    /// The criteria of a stored smart playlist.
    /// </summary>
    public static SmartPlaylistCriteria Of(SmartPlaylist playlist)
    {
        return new SmartPlaylistCriteria(
            playlist.Match,
            playlist.Rules,
            playlist.Sort,
            playlist.Direction,
            playlist.Limit);
    }
}
