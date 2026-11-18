using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Access to the Subsonic accounts of the users and to the queries of the library that only the Subsonic API needs.
/// Nothing is tracked.
/// </summary>
public interface ISubsonicRepository
{
    /// <summary>
    /// The Subsonic account of the user; <c>null</c> if the user never changed it.
    /// </summary>
    Task<SubsonicAccount?> FindAccountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The user with the normalized profile name and its Subsonic account; <c>null</c> if there is no such user.
    /// </summary>
    Task<SubsonicLogin?> FindLoginByNameAsync(string normalizedName, CancellationToken cancellationToken = default);

    /// <summary>
    /// The user whose app password has the hash; <c>null</c> if there is none.
    /// </summary>
    Task<SubsonicLogin?> FindLoginByPasswordHashAsync(
        string passwordHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the Subsonic access of the user on or off and returns the account.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user does not exist.</exception>
    Task<SubsonicAccount> SetEnabledAsync(Guid userId, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new app password of the user, which replaces an earlier one, and returns the account.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user does not exist.</exception>
    Task<SubsonicAccount> SetPasswordAsync(
        Guid userId,
        string protectedPassword,
        string passwordHash,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the app password of the user; nothing happens if there is none.
    /// </summary>
    Task RemovePasswordAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The file, the favorite and the plays of the user for each of the tracks, by id of the track; ids of missing
    /// tracks are left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SubsonicTrackDetails>> ListTrackDetailsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The favorite and the plays of the user for each of the albums, by id of the album.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SubsonicAlbumDetails>> ListAlbumDetailsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> albumIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// When the user marked each of the items of the type as favorite, by id of the item; items that are no favorite
    /// are left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListStarredAtAsync(
        Guid userId,
        FavoriteType type,
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The ids of a page of one of the album lists of the Subsonic API, in the order of the list.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListAlbumIdsAsync(
        SubsonicAlbumListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The ids of random tracks.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListRandomTrackIdsAsync(
        SubsonicRandomTrackQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The ids of the tracks of the artist that belong to no album, by title.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListTrackIdsWithoutAlbumAsync(
        Guid artistId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The names of the users, by id; ids of missing users are left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> ListUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// When the library changed the last time: the latest change of a track or album; <c>null</c> for an empty
    /// library.
    /// </summary>
    Task<DateTimeOffset?> GetLibraryChangedAtAsync(CancellationToken cancellationToken = default);
}
