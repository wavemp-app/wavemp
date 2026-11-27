using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Texts;

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
        IQueryable<Artist> artists = Filter(dbContext.Artists.AsNoTracking(), query);
        Page<ArtistRow> page = await Project(Sort(artists, query).ThenBy(artist => artist.Id), query.UserId)
            .ToPageAsync(query.Offset, query.Limit, cancellationToken);
        return page with { Items = await WithCollagesAsync(page.Items, cancellationToken) };
    }

    public async Task<IReadOnlyList<GroupKeyRow>> ListGroupKeysAsync(
        ArtistListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Artist> artists = Filter(dbContext.Artists.AsNoTracking(), query);
        IOrderedQueryable<Artist> sorted = Sort(artists, query).ThenBy(artist => artist.Id);

        // Only the first character of the name, so the rows of long lists stay small.
        IQueryable<GroupKeyRow> keys = query.Sort == ArtistSortField.Name
            ? sorted.Select(artist => new GroupKeyRow(null, artist.Id, null, artist.Name.Substring(0, 1)))
            : sorted.Select(artist => new GroupKeyRow(null, artist.Id, null, null));
        return await keys.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GroupArtistRow>> ListGroupArtistsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        Guid[] distinct = [.. ids.Distinct()];
        return await dbContext.Artists.AsNoTracking()
            .Where(artist => distinct.Contains(artist.Id))
            .Select(artist => new GroupArtistRow(artist.Id, artist.Name))
            .ToListAsync(cancellationToken);
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

    private IQueryable<Artist> Filter(IQueryable<Artist> artists, ArtistListQuery query)
    {
        if (query.Search is { } search)
        {
            // Also by the normalized name, so "kflay" or "Beyonce" finds "K.Flay" and "Beyoncé".
            string pattern = LikePatterns.Contains(search);
            string keyPattern = NameKeys.Of(search) is { } key ? LikePatterns.Contains(key) : pattern;
            artists = artists.Where(artist =>
                EF.Functions.ILike(artist.Name, pattern, LikePatterns.EscapeCharacter)
                || EF.Functions.Like(artist.NormalizedName, keyPattern, LikePatterns.EscapeCharacter)
                || EF.Functions.TrigramsAreWordSimilar(search, artist.Name));
        }

        if (query.SourceId is { } sourceId)
        {
            IQueryable<Guid> sourceArtists = ArtistIdsOf(dbContext.Tracks.Where(track => track.SourceId == sourceId));
            artists = artists.Where(artist => sourceArtists.Contains(artist.Id));
        }

        if (query.MyMusic)
        {
            // Like the artist filter of the track list: as artist of the track or of its album.
            IQueryable<Track> myTracks = MyMusicQueries.Tracks(
                dbContext,
                MyMusicQueries.RequireUser(query.UserId, "My music"));
            IQueryable<Guid> myArtists = ArtistIdsOf(myTracks);
            artists = artists.Where(artist => myArtists.Contains(artist.Id));
        }

        return artists;
    }

    /// <summary>
    /// The ids of the artists of the tracks or of their albums. Not correlated with the artist, so the database forms
    /// the set once instead of searching the tracks again for every artist of the list.
    /// </summary>
    private IQueryable<Guid> ArtistIdsOf(IQueryable<Track> tracks)
    {
        return dbContext.TrackArtistCredits
            .Where(credit => tracks.Any(track => track.Id == credit.TrackId))
            .Select(credit => credit.ArtistId)
            .Union(dbContext.AlbumArtistCredits
                .Where(credit => tracks.Any(track => track.AlbumId == credit.AlbumId))
                .Select(credit => credit.ArtistId));
    }

    /// <summary>
    /// Sorts by the key in the direction; ties by name (ascending). The caller adds the id as last key.
    /// </summary>
    private static IOrderedQueryable<Artist> Sort(IQueryable<Artist> artists, ArtistListQuery query)
    {
        string? search = query.Search;
        return query.Sort switch
        {
            ArtistSortField.FirstSeenAt => artists
                .OrderBy(artist => artist.FirstSeenAt, query.Direction)
                .ThenBy(artist => artist.NormalizedName),
            ArtistSortField.Relevance when search is not null => artists
                .OrderBy(artist => EF.Functions.TrigramsWordSimilarity(search, artist.Name), query.Direction)
                .ThenBy(artist => artist.NormalizedName),
            _ => artists.OrderBy(artist => artist.NormalizedName, query.Direction),
        };
    }

    /// <summary>
    /// Albums count with the artist as one of their album artists or with tracks of the artist, tracks with the artist
    /// as one of their artists or on albums of the artist, like the artist filters of the album and track lists.
    /// Without a user, no artist is a favorite. The albums of the collage are added afterwards by <see
    /// cref="WithCollagesAsync"/>. Both counts start from the credits of the artist (index on the artist) and count
    /// the union of both ways, so a count reads only the rows of the artist instead of every album or track.
    /// </summary>
    private IQueryable<ArtistRow> Project(IQueryable<Artist> artists, Guid? userId)
    {
        Guid user = userId ?? Guid.Empty;
        return artists.Select(artist => new ArtistRow(
            artist.Id,
            artist.Name,
            dbContext.AlbumArtistCredits
                .Where(credit => credit.ArtistId == artist.Id)
                .Select(credit => credit.AlbumId)
                .Union(dbContext.TrackArtistCredits
                    .Where(credit => credit.ArtistId == artist.Id && credit.Track!.AlbumId != null)
                    .Select(credit => credit.Track!.AlbumId!.Value))
                .Count(),
            dbContext.TrackArtistCredits
                .Where(credit => credit.ArtistId == artist.Id)
                .Select(credit => credit.TrackId)
                .Union(dbContext.AlbumArtistCredits
                    .Where(credit => credit.ArtistId == artist.Id)
                    .SelectMany(credit => credit.Album!.Tracks)
                    .Select(track => track.Id))
                .Count(),
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
    /// the albums of the artist as album artist and the albums with its tracks, each only with a cover of its own (a
    /// picture in its folder, a picture embedded in one of its tracks or an online cover).
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

        // Only albums with a cover of their own: the picture of the artist an album may fall back to is no album cover.
        IQueryable<Album> withCover = dbContext.Albums.AsNoTracking().Where(album =>
            dbContext.Covers.Any(cover =>
                cover.AlbumId == album.Id
                || (cover.Source == CoverSource.Embedded && cover.Track != null && cover.Track.AlbumId == album.Id))
            || dbContext.OnlineCovers.Any(cover => cover.AlbumId == album.Id));
        List<CollageAlbum> asAlbumArtist = await withCover
            .SelectMany(
                album => album.ArtistCredits.Where(credit => ids.Contains(credit.ArtistId)),
                (album, credit) => new CollageAlbum(
                    credit.ArtistId,
                    album.Id,
                    true,
                    album.Year,
                    album.NormalizedTitle))
            .ToListAsync(cancellationToken);
        List<CollageAlbum> withTracks = await withCover
            .SelectMany(
                album => album.Tracks.SelectMany(track => track.ArtistCredits)
                    .Where(credit => ids.Contains(credit.ArtistId)),
                (album, credit) => new CollageAlbum(
                    credit.ArtistId,
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
