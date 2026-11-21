using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Paging and filter of the favorites of a user, the newest first.
/// </summary>
/// <param name="UserId">Id of the user.</param>
/// <param name="Offset">Number of favorites to skip.</param>
/// <param name="Limit">Maximum number of favorites of the page.</param>
/// <param name="Type">Only favorites of this type.</param>
/// <param name="SourceId">Only favorites with tracks of this media source: tracks of the source, albums with at least
/// one track of it, artists with at least one track of it (as artist of the track or of its album).</param>
public sealed record FavoriteListQuery(
    Guid UserId,
    int Offset,
    int Limit,
    FavoriteType? Type = null,
    Guid? SourceId = null);
