using Microsoft.Extensions.Options;
using WaveMP.Core.Collection.Favorites;
using WaveMP.Core.Library.Albums;
using WaveMP.Core.Library.Artists;
using WaveMP.Core.Library.Tracks;
using WaveMP.Core.Metadata.Options;
using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Elements;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Browsing;

internal sealed class SubsonicBrowsingService(
    IArtistService artists,
    IAlbumService albums,
    ITrackService trackService,
    ITrackRepository tracks,
    IFavoriteService favorites,
    ISubsonicRepository repository,
    SubsonicElementLoader loader,
    IOptions<MediaLibraryOptions> mediaOptions) : ISubsonicBrowsingService
{
    /// <summary>
    /// Name of the music folder when the media folder has no name of its own (e.g. the root of a drive).
    /// </summary>
    private const string FallbackFolderName = "media";

    public SubsonicResponse GetMusicFolders()
    {
        string root = mediaOptions.Value.RootPath.TrimEnd('/', '\\');
        string name = Path.GetFileName(root) is { Length: > 0 } folder ? folder : FallbackFolderName;
        return SubsonicResponse.Ok(new SubsonicElement("musicFolders").AddList(
            "musicFolder",
            [new SubsonicElement("musicFolder").Set("id", SubsonicIds.MusicFolderId).Set("name", name)]));
    }

    public async Task<SubsonicResponse> GetIndexesAsync(
        SubsonicUser user,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset? changedAt = await repository.GetLibraryChangedAtAsync(cancellationToken);
        SubsonicElement indexes = new SubsonicElement("indexes")
            .Set("lastModified", changedAt?.ToUnixTimeMilliseconds() ?? 0L)
            .Set("ignoredArticles", SubsonicIndexes.IgnoredArticles);
        indexes.AddList("index", await IndexesAsync(user, cancellationToken));
        return SubsonicResponse.Ok(indexes);
    }

    public async Task<SubsonicResponse> GetArtistsAsync(
        SubsonicUser user,
        CancellationToken cancellationToken = default)
    {
        SubsonicElement result = new SubsonicElement("artists")
            .Set("ignoredArticles", SubsonicIndexes.IgnoredArticles);
        result.AddList("index", await IndexesAsync(user, cancellationToken));
        return SubsonicResponse.Ok(result);
    }

    public async Task<SubsonicResponse> GetArtistAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("id"))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        if (parameters.GetId("id") is not { } id
            || await artists.GetAsync(id, user.Id, cancellationToken) is not { } artist)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        IReadOnlyList<SubsonicElement> artistElements = await loader.ArtistsAsync(
            user.Id,
            [artist],
            cancellationToken: cancellationToken);
        Page<AlbumRow>? page = await artists.ListAlbumsAsync(
            id,
            new AlbumListRequest(0, SubsonicLimits.Unlimited, AlbumSortField.Year, SortDirection.Asc, UserId: user.Id),
            cancellationToken);
        SubsonicElement element = artistElements[0];
        element.AddList("album", await loader.AlbumsAsync(user.Id, page?.Items ?? [], cancellationToken: cancellationToken));
        return SubsonicResponse.Ok(element);
    }

    public async Task<SubsonicResponse> GetAlbumAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("id"))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        if (parameters.GetId("id") is not { } id
            || await albums.GetAsync(id, user.Id, cancellationToken) is not { } album)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        IReadOnlyList<SubsonicElement> albumElements = await loader.AlbumsAsync(
            user.Id,
            [album],
            cancellationToken: cancellationToken);
        Page<TrackRow>? songs = await albums.ListTracksAsync(
            id,
            new TrackListRequest(0, SubsonicLimits.Unlimited, UserId: user.Id),
            cancellationToken);
        SubsonicElement element = albumElements[0];
        element.AddList("song", await loader.SongsAsync(user.Id, songs?.Items ?? [], cancellationToken: cancellationToken));
        return SubsonicResponse.Ok(element);
    }

    public async Task<SubsonicResponse> GetSongAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("id"))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        if (parameters.GetId("id") is not { } id
            || await tracks.FindAsync(id, user.Id, cancellationToken) is not { } track)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        IReadOnlyList<SubsonicElement> songs = await loader.SongsAsync(
            user.Id,
            [track],
            cancellationToken: cancellationToken);
        return SubsonicResponse.Ok(songs[0]);
    }

    public async Task<SubsonicResponse> GetMusicDirectoryAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("id"))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        if (parameters.GetId("id") is not { } id)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        if (await artists.GetAsync(id, user.Id, cancellationToken) is { } artist)
        {
            return SubsonicResponse.Ok(await ArtistDirectoryAsync(user, artist, cancellationToken));
        }

        if (await albums.GetAsync(id, user.Id, cancellationToken) is { } album)
        {
            return SubsonicResponse.Ok(await AlbumDirectoryAsync(user, album, cancellationToken));
        }

        return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
    }

    public async Task<SubsonicResponse> GetAlbumList2Async(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        string? type = parameters.Get("type");
        if (string.IsNullOrEmpty(type))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        int size = SubsonicLimits.Size(
            parameters.GetInt("size"),
            SubsonicLimits.DefaultListSize,
            SubsonicLimits.MaxListSize);
        int offset = SubsonicLimits.Offset(parameters.GetInt("offset"));
        IReadOnlyList<SubsonicElement> items;
        switch (type)
        {
            case "random":
                items = await ByOrderAsync(user, new SubsonicAlbumListQuery(SubsonicAlbumOrder.Random, user.Id, 0, size));
                break;
            case "frequent":
                items = await ByOrderAsync(
                    user,
                    new SubsonicAlbumListQuery(SubsonicAlbumOrder.Frequent, user.Id, offset, size));
                break;
            case "recent":
                items = await ByOrderAsync(
                    user,
                    new SubsonicAlbumListQuery(SubsonicAlbumOrder.Recent, user.Id, offset, size));
                break;
            case "highest":
                items = await ByOrderAsync(
                    user,
                    new SubsonicAlbumListQuery(SubsonicAlbumOrder.Highest, user.Id, offset, size));
                break;
            case "byYear":
                if (parameters.GetInt("fromYear") is not { } fromYear || parameters.GetInt("toYear") is not { } toYear)
                {
                    return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
                }

                items = await ByOrderAsync(
                    user,
                    new SubsonicAlbumListQuery(SubsonicAlbumOrder.Year, user.Id, offset, size, fromYear, toYear));
                break;
            case "newest":
                items = await SortedAsync(user, offset, size, AlbumSortField.FirstSeenAt, SortDirection.Desc);
                break;
            case "alphabeticalByName":
                items = await SortedAsync(user, offset, size, AlbumSortField.Title, SortDirection.Asc);
                break;
            case "alphabeticalByArtist":
                items = await SortedAsync(user, offset, size, AlbumSortField.Artist, SortDirection.Asc);
                break;
            case "byGenre":
                if (string.IsNullOrWhiteSpace(parameters.Get("genre")))
                {
                    return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
                }

                items = await SortedAsync(
                    user,
                    offset,
                    size,
                    AlbumSortField.Artist,
                    SortDirection.Asc,
                    parameters.Get("genre"));
                break;
            case "starred":
                items = await StarredAlbumsAsync(user, offset, size);
                break;
            default:
                return SubsonicResponse.Failed(SubsonicErrorCode.Generic);
        }

        return SubsonicResponse.Ok(new SubsonicElement("albumList2").AddList("album", items));

        async Task<IReadOnlyList<SubsonicElement>> ByOrderAsync(SubsonicUser listUser, SubsonicAlbumListQuery query)
        {
            if (query.Size == 0)
            {
                return [];
            }

            IReadOnlyList<Guid> ids = await repository.ListAlbumIdsAsync(query, cancellationToken);
            return await loader.AlbumsByIdsAsync(listUser.Id, ids, cancellationToken: cancellationToken);
        }

        async Task<IReadOnlyList<SubsonicElement>> SortedAsync(
            SubsonicUser listUser,
            int listOffset,
            int listSize,
            AlbumSortField sort,
            SortDirection direction,
            string? genre = null)
        {
            if (listSize == 0)
            {
                return [];
            }

            Page<AlbumRow> page = await albums.ListAsync(
                new AlbumListRequest(listOffset, listSize, sort, direction, Genre: genre, UserId: listUser.Id),
                cancellationToken);
            return await loader.AlbumsAsync(listUser.Id, page.Items, cancellationToken: cancellationToken);
        }

        async Task<IReadOnlyList<SubsonicElement>> StarredAlbumsAsync(SubsonicUser listUser, int listOffset, int listSize)
        {
            if (listSize == 0)
            {
                return [];
            }

            Page<FavoriteItem> page = await favorites.ListAsync(
                new FavoriteListRequest(listUser.Id, listOffset, listSize, FavoriteType.Album),
                cancellationToken);
            return await loader.AlbumsAsync(
                listUser.Id,
                [.. page.Items.Select(item => item.Album).OfType<AlbumRow>()],
                cancellationToken: cancellationToken);
        }
    }

    public async Task<SubsonicResponse> GetRandomSongsAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        int size = SubsonicLimits.Size(
            parameters.GetInt("size"),
            SubsonicLimits.DefaultListSize,
            SubsonicLimits.MaxListSize);
        IReadOnlyList<SubsonicElement> songs = [];
        if (size > 0)
        {
            string? genre = parameters.Get("genre");
            IReadOnlyList<Guid> ids = await repository.ListRandomTrackIdsAsync(
                new SubsonicRandomTrackQuery(
                    size,
                    string.IsNullOrWhiteSpace(genre) ? null : genre.Trim(),
                    parameters.GetInt("fromYear"),
                    parameters.GetInt("toYear")),
                cancellationToken);
            songs = await loader.SongsByIdsAsync(user.Id, ids, cancellationToken: cancellationToken);
        }

        return SubsonicResponse.Ok(new SubsonicElement("randomSongs").AddList("song", songs));
    }

    public async Task<SubsonicResponse> Search3Async(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (parameters.Get("query") is not { } rawQuery)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        // Apps send "" (with the quotes) to list everything.
        string? query = rawQuery.Replace("\"", string.Empty, StringComparison.Ordinal).Trim() is { Length: > 0 } text
            ? text
            : null;
        int artistCount = Count("artistCount");
        int albumCount = Count("albumCount");
        int songCount = Count("songCount");

        SubsonicElement result = new("searchResult3");
        IReadOnlyList<SubsonicElement> artistItems = [];
        if (artistCount > 0)
        {
            Page<ArtistRow> page = await artists.ListAsync(
                new ArtistListRequest(Offset("artistOffset"), artistCount, Search: query, UserId: user.Id),
                cancellationToken);
            artistItems = await loader.ArtistsAsync(user.Id, page.Items, cancellationToken: cancellationToken);
        }

        IReadOnlyList<SubsonicElement> albumItems = [];
        if (albumCount > 0)
        {
            Page<AlbumRow> page = await albums.ListAsync(
                new AlbumListRequest(Offset("albumOffset"), albumCount, Search: query, UserId: user.Id),
                cancellationToken);
            albumItems = await loader.AlbumsAsync(user.Id, page.Items, cancellationToken: cancellationToken);
        }

        IReadOnlyList<SubsonicElement> songItems = [];
        if (songCount > 0)
        {
            Page<TrackRow> page = await trackService.ListAsync(
                new TrackListRequest(Offset("songOffset"), songCount, Search: query, UserId: user.Id),
                cancellationToken);
            songItems = await loader.SongsAsync(user.Id, page.Items, cancellationToken: cancellationToken);
        }

        result.AddList("artist", artistItems).AddList("album", albumItems).AddList("song", songItems);
        return SubsonicResponse.Ok(result);

        int Count(string name)
        {
            return SubsonicLimits.Size(
                parameters.GetInt(name),
                SubsonicLimits.DefaultSearchCount,
                SubsonicLimits.MaxSearchCount);
        }

        int Offset(string name)
        {
            return SubsonicLimits.Offset(parameters.GetInt(name));
        }
    }

    public async Task<SubsonicResponse> GetStarred2Async(
        SubsonicUser user,
        CancellationToken cancellationToken = default)
    {
        Page<FavoriteItem> artistFavorites = await favorites.ListAsync(
            new FavoriteListRequest(user.Id, 0, SubsonicLimits.Unlimited, FavoriteType.Artist),
            cancellationToken);
        Page<FavoriteItem> albumFavorites = await favorites.ListAsync(
            new FavoriteListRequest(user.Id, 0, SubsonicLimits.Unlimited, FavoriteType.Album),
            cancellationToken);
        Page<FavoriteItem> trackFavorites = await favorites.ListAsync(
            new FavoriteListRequest(user.Id, 0, SubsonicLimits.Unlimited, FavoriteType.Track),
            cancellationToken);

        SubsonicElement result = new("starred2");
        result.AddList(
            "artist",
            await loader.ArtistsAsync(
                user.Id,
                [.. artistFavorites.Items.Select(item => item.Artist).OfType<ArtistRow>()],
                cancellationToken: cancellationToken));
        result.AddList(
            "album",
            await loader.AlbumsAsync(
                user.Id,
                [.. albumFavorites.Items.Select(item => item.Album).OfType<AlbumRow>()],
                cancellationToken: cancellationToken));
        result.AddList(
            "song",
            await loader.SongsAsync(
                user.Id,
                [.. trackFavorites.Items.Select(item => item.Track).OfType<TrackRow>()],
                cancellationToken: cancellationToken));
        return SubsonicResponse.Ok(result);
    }

    /// <summary>
    /// All artists, grouped into indexes.
    /// </summary>
    private async Task<IReadOnlyList<SubsonicElement>> IndexesAsync(
        SubsonicUser user,
        CancellationToken cancellationToken)
    {
        Page<ArtistRow> page = await artists.ListAsync(
            new ArtistListRequest(0, SubsonicLimits.Unlimited, UserId: user.Id),
            cancellationToken);
        IReadOnlyDictionary<Guid, DateTimeOffset> stars = await repository.ListStarredAtAsync(
            user.Id,
            FavoriteType.Artist,
            [.. page.Items.Select(artist => artist.Id)],
            cancellationToken);
        return [.. SubsonicIndexes.Group(page.Items, artist => artist.Name)
            .Select(group => new SubsonicElement("index")
                .Set("name", group.Index)
                .AddList(
                    "artist",
                    group.Items.Select(artist => SubsonicElements.Artist(
                        artist,
                        SubsonicElementLoader.StarredAt(stars, artist.Id)))))];
    }

    private async Task<SubsonicElement> ArtistDirectoryAsync(
        SubsonicUser user,
        ArtistRow artist,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, DateTimeOffset> stars = await repository.ListStarredAtAsync(
            user.Id,
            FavoriteType.Artist,
            [artist.Id],
            cancellationToken);
        Page<AlbumRow>? page = await artists.ListAlbumsAsync(
            artist.Id,
            new AlbumListRequest(0, SubsonicLimits.Unlimited, AlbumSortField.Year, SortDirection.Asc, UserId: user.Id),
            cancellationToken);
        IReadOnlyList<AlbumRow> artistAlbums = page?.Items ?? [];
        IReadOnlyDictionary<Guid, SubsonicAlbumDetails> details = await repository.ListAlbumDetailsAsync(
            user.Id,
            [.. artistAlbums.Select(album => album.Id)],
            cancellationToken);
        IReadOnlyList<Guid> looseTracks = await repository.ListTrackIdsWithoutAlbumAsync(artist.Id, cancellationToken);
        IReadOnlyList<SubsonicElement> songs = await loader.SongsByIdsAsync(
            user.Id,
            looseTracks,
            "child",
            cancellationToken);

        return new SubsonicElement("directory")
            .Set("id", SubsonicIds.Format(artist.Id))
            .Set("name", artist.Name)
            .Set("starred", SubsonicElementLoader.StarredAt(stars, artist.Id))
            .AddList(
                "child",
                [
                    .. artistAlbums.Select(album =>
                        SubsonicElements.AlbumFolder(album, details.GetValueOrDefault(album.Id), artist.Id)),
                    .. songs,
                ]);
    }

    private async Task<SubsonicElement> AlbumDirectoryAsync(
        SubsonicUser user,
        AlbumRow album,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, SubsonicAlbumDetails> details = await repository.ListAlbumDetailsAsync(
            user.Id,
            [album.Id],
            cancellationToken);
        SubsonicAlbumDetails? albumDetails = details.GetValueOrDefault(album.Id);
        Page<TrackRow>? songs = await albums.ListTracksAsync(
            album.Id,
            new TrackListRequest(0, SubsonicLimits.Unlimited, UserId: user.Id),
            cancellationToken);
        return new SubsonicElement("directory")
            .Set("id", SubsonicIds.Format(album.Id))
            .Set("parent", album.ArtistId is { } artistId ? SubsonicIds.Format(artistId) : null)
            .Set("name", album.Title)
            .Set("starred", albumDetails?.StarredAt)
            .Set("playCount", (long)(albumDetails?.PlayCount ?? 0))
            .AddList(
                "child",
                await loader.SongsAsync(user.Id, songs?.Items ?? [], "child", cancellationToken));
    }
}
