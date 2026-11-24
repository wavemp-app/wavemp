using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

internal sealed class ArtistRepository(WaveMpDbContext dbContext) : IArtistRepository
{
    /// <summary>
    /// Largest number of album covers of the collage of an artist without a picture of its own.
    /// </summary>
    public const int MaxCollageAlbums = 4;

    public async Task<Page<ArtistRow>> ListAsync(
        ArtistListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Artist> artists = dbContext.Artists.AsNoTracking();
        string? search = query.Search;
        if (search is not null)
        {
            string pattern = LikePatterns.Contains(search);
            artists = artists.Where(artist =>
                EF.Functions.ILike(artist.Name, pattern, LikePatterns.EscapeCharacter)
                || EF.Functions.TrigramsAreWordSimilar(search, artist.Name));
        }

        if (query.SourceId is { } sourceId)
        {
            artists = artists.Where(artist => dbContext.Tracks.Any(track => track.SourceId == sourceId
                && (track.ArtistId == artist.Id || (track.Album != null && track.Album.AlbumArtistId == artist.Id))));
        }

        if (query.MyMusic)
        {
            // Like the artist filter of the track list: as artist of the track or of its album.
            IQueryable<Track> myTracks = MyMusicQueries.Tracks(
                dbContext,
                MyMusicQueries.RequireUser(query.UserId, "My music"));
            artists = artists.Where(artist => myTracks.Any(track =>
                track.ArtistId == artist.Id || (track.Album != null && track.Album.AlbumArtistId == artist.Id)));
        }

        IOrderedQueryable<Artist> sorted = query.Sort switch
        {
            ArtistSortField.FirstSeenAt => artists
                .OrderBy(artist => artist.FirstSeenAt, query.Direction)
                .ThenBy(artist => artist.NormalizedName),
            ArtistSortField.Relevance when search is not null => artists
                .OrderBy(artist => EF.Functions.TrigramsWordSimilarity(search, artist.Name), query.Direction)
                .ThenBy(artist => artist.NormalizedName),
            _ => artists.OrderBy(artist => artist.NormalizedName, query.Direction),
        };

        Page<ArtistRow> page = await Project(sorted.ThenBy(artist => artist.Id), query.UserId)
            .ToPageAsync(query.Offset, query.Limit, cancellationToken);
        return page with { Items = await WithCollagesAsync(page.Items, cancellationToken) };
    }

    public async Task<ArtistRow?> FindAsync(
        Guid id,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArtistRow? artist = await Project(dbContext.Artists.AsNoTracking().Where(artist => artist.Id == id), userId)
            .FirstOrDefaultAsync(cancellationToken);
        return artist is null ? null : (await WithCollagesAsync([artist], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<ArtistRow>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        Guid[] distinct = [.. ids.Distinct()];
        List<ArtistRow> rows = await Project(
                dbContext.Artists.AsNoTracking().Where(artist => distinct.Contains(artist.Id)),
                userId)
            .ToListAsync(cancellationToken);
        return await WithCollagesAsync(rows, cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Artists.AnyAsync(artist => artist.Id == id, cancellationToken);
    }

    /// <summary>
    /// Albums count with the artist as album artist or with tracks of the artist, tracks with the artist or on albums
    /// of the artist, like the artist filters of the album and track lists. Without a user, no artist is a favorite.
    /// The albums of the collage are added afterwards by <see cref="WithCollagesAsync"/>.
    /// </summary>
    private IQueryable<ArtistRow> Project(IQueryable<Artist> artists, Guid? userId)
    {
        Guid user = userId ?? Guid.Empty;
        return artists.Select(artist => new ArtistRow(
            artist.Id,
            artist.Name,
            dbContext.Albums.Count(album =>
                album.AlbumArtistId == artist.Id || album.Tracks.Any(track => track.ArtistId == artist.Id)),
            dbContext.Tracks.Count(track =>
                track.ArtistId == artist.Id || (track.Album != null && track.Album.AlbumArtistId == artist.Id)),
            artist.FirstSeenAt,
            dbContext.Favorites.Any(favorite => favorite.UserId == user && favorite.ArtistId == artist.Id),
            new ArtistPictureRow(
                artist.LocalPictureFileId,
                artist.LocalPictureFile != null ? artist.LocalPictureFile.ModifiedAt : null,
                artist.Pictures
                    .Where(picture => picture.Kind == ArtistPictureKind.Uploaded)
                    .Select(picture => (Guid?)picture.Id)
                    .FirstOrDefault(),
                artist.Pictures
                    .Where(picture => picture.Kind == ArtistPictureKind.Online && picture.Provider != null)
                    .Select(picture => new ArtistOnlinePictureRow(
                        picture.Id,
                        picture.Provider!.Value,
                        picture.Author,
                        picture.License,
                        picture.LicenseUrl,
                        picture.SourceUrl))
                    .FirstOrDefault(),
                artist.MusicBrainzArtistId)));
    }

    /// <summary>
    /// Adds the albums of the collage to the artists without a picture of their own, with two queries for all of them:
    /// the albums of the artist as album artist and the albums with its tracks, each only with a cover (of the album
    /// or of one of its tracks, from the library or online).
    /// </summary>
    private async Task<IReadOnlyList<ArtistRow>> WithCollagesAsync(
        IReadOnlyList<ArtistRow> rows,
        CancellationToken cancellationToken)
    {
        Guid[] ids = [.. rows
            .Where(row => row.Picture is { HasOwnPicture: false })
            .Select(row => row.Id)
            .Distinct()];
        if (ids.Length == 0)
        {
            return rows;
        }

        IQueryable<Album> withCover = dbContext.Albums.AsNoTracking().Where(album =>
            dbContext.Covers.Any(cover =>
                cover.AlbumId == album.Id || (cover.Track != null && cover.Track.AlbumId == album.Id))
            || dbContext.OnlineCovers.Any(cover =>
                cover.AlbumId == album.Id || (cover.Track != null && cover.Track.AlbumId == album.Id)));
        List<CollageAlbum> asAlbumArtist = await withCover
            .Where(album => album.AlbumArtistId != null && ids.Contains(album.AlbumArtistId.Value))
            .Select(album => new CollageAlbum(
                album.AlbumArtistId!.Value,
                album.Id,
                true,
                album.Year,
                album.NormalizedTitle))
            .ToListAsync(cancellationToken);
        List<CollageAlbum> withTracks = await withCover
            .SelectMany(
                album => album.Tracks.Where(track => track.ArtistId != null && ids.Contains(track.ArtistId.Value)),
                (album, track) => new CollageAlbum(
                    track.ArtistId!.Value,
                    album.Id,
                    false,
                    album.Year,
                    album.NormalizedTitle))
            .Distinct()
            .ToListAsync(cancellationToken);

        var collages = asAlbumArtist
            .Concat(withTracks)
            .GroupBy(album => album.ArtistId)
            .ToDictionary(group => group.Key, group => OrderForCollage(group));
        return [.. rows.Select(row =>
            row.Picture is { HasOwnPicture: false } picture && collages.TryGetValue(row.Id, out Guid[]? albumIds)
                ? row with { Picture = picture with { CollageAlbumIds = albumIds } }
                : row)];
    }

    /// <summary>
    /// The albums of the collage of one artist: each album once, albums with the artist as album artist first, then
    /// the newest year first (albums without year last), then by title and id.
    /// </summary>
    private static Guid[] OrderForCollage(IEnumerable<CollageAlbum> albums)
    {
        return [.. albums
            .GroupBy(album => album.AlbumId)
            .Select(same => same.OrderByDescending(album => album.IsAlbumArtist).First())
            .OrderByDescending(album => album.IsAlbumArtist)
            .ThenBy(album => album.Year is null)
            .ThenByDescending(album => album.Year)
            .ThenBy(album => album.NormalizedTitle, StringComparer.Ordinal)
            .ThenBy(album => album.AlbumId)
            .Take(MaxCollageAlbums)
            .Select(album => album.AlbumId)];
    }

    private sealed record CollageAlbum(
        Guid ArtistId,
        Guid AlbumId,
        bool IsAlbumArtist,
        int? Year,
        string NormalizedTitle);
}
