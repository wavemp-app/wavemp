using WaveMP.Data.Entities;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// The built-in templates of smart playlists, in the order clients offer them.
/// </summary>
public static class SmartPlaylistTemplates
{
    /// <summary>
    /// Days of the template <see cref="SmartPlaylistTemplateId.RecentlyAdded"/>.
    /// </summary>
    public const int RecentlyAddedDays = 30;

    /// <summary>
    /// Limit of the template <see cref="SmartPlaylistTemplateId.MostPlayed"/>.
    /// </summary>
    public const int MostPlayedLimit = 50;

    public static IReadOnlyList<SmartPlaylistTemplate> All { get; } =
    [
        new(
            SmartPlaylistTemplateId.FourStarsAndMore,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [new SmartPlaylistRule(SmartPlaylistField.Rating, SmartPlaylistOperator.AtLeast, Number: 4)],
                SmartPlaylistSortField.Rating,
                SortDirection.Desc)),
        new(
            SmartPlaylistTemplateId.NeverPlayed,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [new SmartPlaylistRule(SmartPlaylistField.PlayCount, SmartPlaylistOperator.Is, Number: 0)],
                SmartPlaylistSortField.DateAdded,
                SortDirection.Desc)),
        new(
            SmartPlaylistTemplateId.RecentlyAdded,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [
                    new SmartPlaylistRule(
                        SmartPlaylistField.DateAdded,
                        SmartPlaylistOperator.InLast,
                        Number: RecentlyAddedDays),
                ],
                SmartPlaylistSortField.DateAdded,
                SortDirection.Desc)),
        new(
            SmartPlaylistTemplateId.MostPlayed,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [new SmartPlaylistRule(SmartPlaylistField.PlayCount, SmartPlaylistOperator.AtLeast, Number: 1)],
                SmartPlaylistSortField.PlayCount,
                SortDirection.Desc,
                MostPlayedLimit)),
        new(
            SmartPlaylistTemplateId.Favorites,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [new SmartPlaylistRule(SmartPlaylistField.Favorite, SmartPlaylistOperator.IsTrue)],
                SmartPlaylistSortField.Artist,
                SortDirection.Asc)),
    ];

    public static SmartPlaylistTemplate Get(SmartPlaylistTemplateId id)
    {
        return All.FirstOrDefault(template => template.Id == id)
            ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown template.");
    }
}
