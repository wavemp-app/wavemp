using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

internal sealed class SubsonicRepository(WaveMpDbContext dbContext) : ISubsonicRepository
{
    public Task<SubsonicAccount?> FindAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.SubsonicAccounts.AsNoTracking()
            .FirstOrDefaultAsync(account => account.UserId == userId, cancellationToken);
    }

    public Task<SubsonicLogin?> FindLoginByNameAsync(
        string normalizedName,
        CancellationToken cancellationToken = default)
    {
        return Logins(dbContext.Users.AsNoTracking().Where(user => user.NormalizedName == normalizedName))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<SubsonicLogin?> FindLoginByPasswordHashAsync(
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Guid> owners = dbContext.SubsonicAccounts
            .Where(account => account.PasswordHash == passwordHash)
            .Select(account => account.UserId);
        return Logins(dbContext.Users.AsNoTracking().Where(user => owners.Contains(user.Id)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<SubsonicAccount> SetEnabledAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(userId, account => account.Enabled = enabled, cancellationToken);
    }

    public Task<SubsonicAccount> SetPasswordAsync(
        Guid userId,
        string protectedPassword,
        string passwordHash,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(
            userId,
            account =>
            {
                account.ProtectedPassword = protectedPassword;
                account.PasswordHash = passwordHash;
                account.PasswordCreatedAt = createdAt;
            },
            cancellationToken);
    }

    public Task RemovePasswordAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.SubsonicAccounts
            .Where(account => account.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(account => account.ProtectedPassword, (string?)null)
                    .SetProperty(account => account.PasswordHash, (string?)null)
                    .SetProperty(account => account.PasswordCreatedAt, (DateTimeOffset?)null),
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, SubsonicTrackDetails>> ListTrackDetailsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        if (trackIds.Count == 0)
        {
            return new Dictionary<Guid, SubsonicTrackDetails>();
        }

        Guid[] ids = [.. trackIds.Distinct()];
        List<Guid> existing = await dbContext.Tracks.AsNoTracking()
            .Where(track => ids.Contains(track.Id))
            .Select(track => track.Id)
            .ToListAsync(cancellationToken);

        List<TrackFile> files = await dbContext.MediaFiles.AsNoTracking()
            .Where(file => file.TrackId != null
                && ids.Contains(file.TrackId.Value)
                && file.IsPrimary
                && (file.Kind == MediaFileKind.Audio || file.Kind == MediaFileKind.Video))
            .Select(file => new TrackFile(
                file.TrackId!.Value,
                new SubsonicFileRow(
                    file.RelativePath,
                    file.Extension,
                    file.Kind,
                    file.Size,
                    file.Bitrate,
                    file.SampleRate,
                    file.BitDepth,
                    file.Channels)))
            .ToListAsync(cancellationToken);

        // The sound of a track comes from its audio file; only tracks without one play the sound of their video.
        var soundFiles = files
            .GroupBy(file => file.TrackId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(file => file.Row.Kind == MediaFileKind.Audio ? 0 : 1)
                    .ThenBy(file => file.Row.RelativePath, StringComparer.Ordinal)
                    .First()
                    .Row);

        Dictionary<Guid, Plays> plays = await dbContext.PlayHistory.AsNoTracking()
            .Where(entry => entry.UserId == userId && ids.Contains(entry.TrackId))
            .GroupBy(entry => entry.TrackId)
            .Select(group => new Plays
            {
                Id = group.Key,
                Count = group.Count(),
                Last = group.Max(entry => entry.PlayedAt),
            })
            .ToDictionaryAsync(play => play.Id, cancellationToken);

        IReadOnlyDictionary<Guid, DateTimeOffset> stars =
            await ListStarredAtAsync(userId, FavoriteType.Track, ids, cancellationToken);

        return existing.ToDictionary(
            id => id,
            id => new SubsonicTrackDetails(
                soundFiles.GetValueOrDefault(id),
                stars.TryGetValue(id, out DateTimeOffset starredAt) ? starredAt : null,
                plays.TryGetValue(id, out Plays? play) ? play.Count : 0,
                play?.Last));
    }

    public async Task<IReadOnlyDictionary<Guid, SubsonicAlbumDetails>> ListAlbumDetailsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> albumIds,
        CancellationToken cancellationToken = default)
    {
        if (albumIds.Count == 0)
        {
            return new Dictionary<Guid, SubsonicAlbumDetails>();
        }

        Guid[] ids = [.. albumIds.Distinct()];
        Dictionary<Guid, Plays> plays = await dbContext.PlayHistory.AsNoTracking()
            .Where(entry => entry.UserId == userId
                && entry.Track!.AlbumId != null
                && ids.Contains(entry.Track.AlbumId.Value))
            .GroupBy(entry => entry.Track!.AlbumId!.Value)
            .Select(group => new Plays
            {
                Id = group.Key,
                Count = group.Count(),
                Last = group.Max(entry => entry.PlayedAt),
            })
            .ToDictionaryAsync(play => play.Id, cancellationToken);

        IReadOnlyDictionary<Guid, DateTimeOffset> stars =
            await ListStarredAtAsync(userId, FavoriteType.Album, ids, cancellationToken);

        return ids.ToDictionary(
            id => id,
            id => new SubsonicAlbumDetails(
                stars.TryGetValue(id, out DateTimeOffset starredAt) ? starredAt : null,
                plays.TryGetValue(id, out Plays? play) ? play.Count : 0,
                play?.Last));
    }

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListStarredAtAsync(
        Guid userId,
        FavoriteType type,
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
        {
            return new Dictionary<Guid, DateTimeOffset>();
        }

        Guid[] ids = [.. itemIds.Distinct()];
        IQueryable<Favorite> favorites = dbContext.Favorites.AsNoTracking()
            .Where(favorite => favorite.UserId == userId && favorite.Type == type);
        IQueryable<Starred> starred = type switch
        {
            FavoriteType.Track => favorites
                .Where(favorite => favorite.TrackId != null && ids.Contains(favorite.TrackId.Value))
                .Select(favorite => new Starred { Id = favorite.TrackId!.Value, CreatedAt = favorite.CreatedAt }),
            FavoriteType.Album => favorites
                .Where(favorite => favorite.AlbumId != null && ids.Contains(favorite.AlbumId.Value))
                .Select(favorite => new Starred { Id = favorite.AlbumId!.Value, CreatedAt = favorite.CreatedAt }),
            _ => favorites
                .Where(favorite => favorite.ArtistId != null && ids.Contains(favorite.ArtistId.Value))
                .Select(favorite => new Starred { Id = favorite.ArtistId!.Value, CreatedAt = favorite.CreatedAt }),
        };

        return await starred.ToDictionaryAsync(row => row.Id, row => row.CreatedAt, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListAlbumIdsAsync(
        SubsonicAlbumListQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid userId = query.UserId;
        IQueryable<Guid> ids;
        switch (query.Order)
        {
            case SubsonicAlbumOrder.Random:
                return await dbContext.Albums.AsNoTracking()
                    .OrderBy(_ => EF.Functions.Random())
                    .Take(query.Size)
                    .Select(album => album.Id)
                    .ToListAsync(cancellationToken);
            case SubsonicAlbumOrder.Frequent or SubsonicAlbumOrder.Recent:
                IQueryable<Plays> plays = dbContext.PlayHistory
                    .Where(entry => entry.UserId == userId && entry.Track!.AlbumId != null)
                    .GroupBy(entry => entry.Track!.AlbumId!.Value)
                    .Select(group => new Plays
                    {
                        Id = group.Key,
                        Count = group.Count(),
                        Last = group.Max(entry => entry.PlayedAt),
                    });
                ids = query.Order == SubsonicAlbumOrder.Frequent
                    ? plays
                        .OrderByDescending(play => play.Count)
                        .ThenByDescending(play => play.Last)
                        .ThenBy(play => play.Id)
                        .Select(play => play.Id)
                    : plays
                        .OrderByDescending(play => play.Last)
                        .ThenBy(play => play.Id)
                        .Select(play => play.Id);
                break;
            case SubsonicAlbumOrder.Highest:
                ids = dbContext.TrackRatings
                    .Where(rating => rating.UserId == userId && rating.Track!.AlbumId != null)
                    .GroupBy(rating => rating.Track!.AlbumId!.Value)
                    .Select(group => new Rated
                    {
                        Id = group.Key,
                        Average = group.Average(rating => (double)rating.Rating),
                        Count = group.Count(),
                    })
                    .OrderByDescending(album => album.Average)
                    .ThenByDescending(album => album.Count)
                    .ThenBy(album => album.Id)
                    .Select(album => album.Id);
                break;
            default:
                int from = Math.Min(query.FromYear, query.ToYear);
                int to = Math.Max(query.FromYear, query.ToYear);
                IQueryable<Album> albums = dbContext.Albums
                    .Where(album => album.Year != null && album.Year >= from && album.Year <= to);
                ids = (query.FromYear <= query.ToYear
                        ? albums.OrderBy(album => album.Year)
                        : albums.OrderByDescending(album => album.Year))
                    .ThenBy(album => album.NormalizedTitle)
                    .ThenBy(album => album.Id)
                    .Select(album => album.Id);
                break;
        }

        return await ids.Skip(query.Offset).Take(query.Size).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListRandomTrackIdsAsync(
        SubsonicRandomTrackQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Track> tracks = dbContext.Tracks.AsNoTracking();
        if (query.Genre is { } genre)
        {
            string pattern = LikePatterns.Escape(genre);
            tracks = tracks.Where(track =>
                track.Genre != null && EF.Functions.ILike(track.Genre, pattern, LikePatterns.EscapeCharacter));
        }

        if (query.FromYear is { } fromYear)
        {
            tracks = tracks.Where(track => track.Year != null && track.Year >= fromYear);
        }

        if (query.ToYear is { } toYear)
        {
            tracks = tracks.Where(track => track.Year != null && track.Year <= toYear);
        }

        return await tracks
            .OrderBy(_ => EF.Functions.Random())
            .Take(query.Size)
            .Select(track => track.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListTrackIdsWithoutAlbumAsync(
        Guid artistId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Tracks.AsNoTracking()
            .Where(track => track.ArtistId == artistId && track.AlbumId == null)
            .OrderBy(track => track.Title)
            .ThenBy(track => track.Id)
            .Select(track => track.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ListUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        Guid[] ids = [.. userIds.Distinct()];
        return await dbContext.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Name, cancellationToken);
    }

    public async Task<DateTimeOffset?> GetLibraryChangedAtAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset? tracks = await dbContext.Tracks.MaxAsync(
            track => (DateTimeOffset?)track.UpdatedAt,
            cancellationToken);
        DateTimeOffset? albums = await dbContext.Albums.MaxAsync(
            album => (DateTimeOffset?)album.UpdatedAt,
            cancellationToken);
        return albums is null || tracks > albums ? tracks : albums;
    }

    private IQueryable<SubsonicLogin> Logins(IQueryable<User> users)
    {
        return users.Select(user => new SubsonicLogin(
            user.Id,
            user.Name,
            dbContext.SubsonicAccounts.Any(account => account.UserId == user.Id && account.Enabled),
            dbContext.SubsonicAccounts
                .Where(account => account.UserId == user.Id)
                .Select(account => account.ProtectedPassword)
                .FirstOrDefault()));
    }

    private Task<SubsonicAccount> SaveAsync(
        Guid userId,
        Action<SubsonicAccount> change,
        CancellationToken cancellationToken)
    {
        return dbContext.InUserTransactionAsync(
            userId,
            async token =>
            {
                // The lock of the user also serializes two changes of the same account at the same time.
                SubsonicAccount? account = await dbContext.SubsonicAccounts
                    .FirstOrDefaultAsync(stored => stored.UserId == userId, token);
                if (account is null)
                {
                    account = new SubsonicAccount { UserId = userId };
                    dbContext.SubsonicAccounts.Add(account);
                }

                change(account);
                try
                {
                    await dbContext.SaveChangesAsync(token);
                }
                finally
                {
                    dbContext.Entry(account).State = EntityState.Detached;
                }

                return account;
            },
            cancellationToken);
    }

    private sealed record TrackFile(Guid TrackId, SubsonicFileRow Row);

    private sealed class Plays
    {
        public Guid Id { get; init; }

        public int Count { get; init; }

        public DateTimeOffset Last { get; init; }
    }

    private sealed class Rated
    {
        public Guid Id { get; init; }

        public double Average { get; init; }

        public int Count { get; init; }
    }

    private sealed class Starred
    {
        public Guid Id { get; init; }

        public DateTimeOffset CreatedAt { get; init; }
    }
}
