using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

internal sealed class LibraryScanRepository(WaveMpDbContext dbContext) : ILibraryScanRepository
{
    public async Task<IReadOnlyList<MediaFileSnapshot>> GetFileSnapshotsAsync(
        Guid sourceId,
        IReadOnlyCollection<string>? directories,
        CancellationToken cancellationToken = default)
    {
        IQueryable<MediaFile> files = dbContext.MediaFiles.AsNoTracking().Where(file => file.SourceId == sourceId);
        if (directories is null || directories.Contains(string.Empty))
        {
            return await ToSnapshots(files).ToListAsync(cancellationToken);
        }

        Dictionary<Guid, MediaFileSnapshot> snapshots = [];
        foreach (string directory in directories)
        {
            string prefix = directory + "/";
            List<MediaFileSnapshot> inDirectory = await ToSnapshots(files.Where(file =>
                    file.DirectoryPath == directory || file.DirectoryPath.StartsWith(prefix)))
                .ToListAsync(cancellationToken);
            foreach (MediaFileSnapshot snapshot in inDirectory)
            {
                snapshots[snapshot.Id] = snapshot;
            }
        }

        return [.. snapshots.Values];
    }

    public async Task<IReadOnlyList<MediaFileSnapshot>> GetFolderSnapshotsAsync(
        Guid sourceId,
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        return await ToSnapshots(dbContext.MediaFiles.AsNoTracking()
                .Where(file => file.SourceId == sourceId && file.DirectoryPath == directoryPath))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountFilesAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        return dbContext.MediaFiles.CountAsync(file => file.SourceId == sourceId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetSourcesToRegroupAsync(
        int version,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MediaSources.AsNoTracking()
            .Where(source => source.AlbumGroupingVersion < version)
            .OrderBy(source => source.Id)
            .Select(source => source.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetTrackDirectoriesAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Tracks.AsNoTracking()
            .Where(track => track.SourceId == sourceId)
            .Select(track => track.DirectoryPath)
            .Distinct()
            .OrderBy(directory => directory)
            .ToListAsync(cancellationToken);
    }

    public async Task SetAlbumGroupingVersionAsync(
        Guid sourceId,
        int version,
        CancellationToken cancellationToken = default)
    {
        await dbContext.MediaSources
            .Where(source => source.Id == sourceId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(source => source.AlbumGroupingVersion, version),
                cancellationToken);
    }

    public async Task<StoredFolder> LoadFolderAsync(
        Guid sourceId,
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        List<MediaFile> files = await dbContext.MediaFiles
            .Where(file => file.SourceId == sourceId && file.DirectoryPath == directoryPath)
            .ToListAsync(cancellationToken);
        List<Track> tracks = await dbContext.Tracks
            .Include(track => track.Covers)
            .Include(track => track.FileSelections)
            .Where(track => track.SourceId == sourceId && track.DirectoryPath == directoryPath)
            .ToListAsync(cancellationToken);
        return new StoredFolder(files, tracks);
    }

    public async Task<IReadOnlyList<Track>> LoadTracksAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        if (trackIds.Count == 0)
        {
            return [];
        }

        Guid[] ids = [.. trackIds.Distinct()];
        return await dbContext.Tracks
            .Include(track => track.Files)
            .Include(track => track.Covers)
            .Include(track => track.FileSelections)
            .Where(track => ids.Contains(track.Id))
            .OrderBy(track => track.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Artist?> FindArtistAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        return dbContext.Artists.FirstOrDefaultAsync(
            artist => artist.NormalizedName == normalizedName,
            cancellationToken);
    }

    public Task<Album?> FindAlbumAsync(string albumKey, CancellationToken cancellationToken = default)
    {
        return dbContext.Albums.FirstOrDefaultAsync(album => album.AlbumKey == albumKey, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetAlbumArtistIdsAsync(
        IReadOnlyCollection<Guid> albumIds,
        CancellationToken cancellationToken = default)
    {
        if (albumIds.Count == 0)
        {
            return new Dictionary<Guid, Guid?>();
        }

        Guid[] ids = [.. albumIds.Distinct()];
        return await dbContext.Albums.AsNoTracking()
            .Where(album => ids.Contains(album.Id))
            .ToDictionaryAsync(album => album.Id, album => album.AlbumArtistId, cancellationToken);
    }

    public void Add<TEntity>(TEntity entity)
        where TEntity : class
    {
        dbContext.Add(entity);
    }

    public void Remove<TEntity>(TEntity entity)
        where TEntity : class
    {
        dbContext.Remove(entity);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    public async Task RecordAlbumMovesAsync(
        IReadOnlyDictionary<(Guid FromAlbumId, Guid ToAlbumId), int> moves,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        foreach (((Guid fromAlbumId, Guid toAlbumId), int trackCount) in moves)
        {
            if (fromAlbumId == toAlbumId || trackCount < 1)
            {
                continue;
            }

            // Finds a move tracked by an earlier call before the save as well as a stored one.
            AlbumMove? move = await dbContext.AlbumMoves.FindAsync([fromAlbumId, toAlbumId], cancellationToken);
            if (move is null)
            {
                dbContext.AlbumMoves.Add(new AlbumMove
                {
                    FromAlbumId = fromAlbumId,
                    ToAlbumId = toAlbumId,
                    TrackCount = trackCount,
                    MovedAt = now,
                });
            }
            else
            {
                move.TrackCount += trackCount;
                move.MovedAt = now;
            }
        }
    }

    public async Task RecordArtistMovesAsync(
        IReadOnlyDictionary<(Guid FromArtistId, Guid ToArtistId), int> moves,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        foreach (((Guid fromArtistId, Guid toArtistId), int trackCount) in moves)
        {
            if (fromArtistId == toArtistId || trackCount < 1)
            {
                continue;
            }

            // Finds a move tracked by an earlier call before the save as well as a stored one.
            ArtistMove? move = await dbContext.ArtistMoves.FindAsync([fromArtistId, toArtistId], cancellationToken);
            if (move is null)
            {
                dbContext.ArtistMoves.Add(new ArtistMove
                {
                    FromArtistId = fromArtistId,
                    ToArtistId = toArtistId,
                    TrackCount = trackCount,
                    MovedAt = now,
                });
            }
            else
            {
                move.TrackCount += trackCount;
                move.MovedAt = now;
            }
        }
    }

    public Task<(int Files, int Tracks)> DeleteDirectoriesAsync(
        Guid sourceId,
        IReadOnlyCollection<string> directories,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (directories.Count == 0)
        {
            return Task.FromResult((0, 0));
        }

        string[] paths = [.. directories];
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await RecordFolderMovesAsync(sourceId, paths, now, token);
                int files = await dbContext.MediaFiles
                    .Where(file => file.SourceId == sourceId && paths.Contains(file.DirectoryPath))
                    .ExecuteDeleteAsync(token);
                int tracks = await dbContext.Tracks
                    .Where(track => track.SourceId == sourceId && paths.Contains(track.DirectoryPath))
                    .ExecuteDeleteAsync(token);
                await transaction.CommitAsync(token);
                return (files, tracks);
            },
            cancellationToken);
    }

    public Task<int> DeleteVanishedTracksAsync(
        IReadOnlyCollection<VanishedTrack> tracks,
        CancellationToken cancellationToken = default)
    {
        if (tracks.Count == 0)
        {
            return Task.FromResult(0);
        }

        Guid[] trackIds = [.. tracks.Select(track => track.TrackId)];
        Guid[] trackSourceIds = [.. tracks.Select(track => track.SourceId)];
        string[] directories = [.. tracks.Select(track => track.DirectoryPath)];
        Guid[] fileIds = [.. tracks.SelectMany(track => track.Files.Select(file => file.FileId))];
        Guid[] fileSourceIds = [.. tracks.SelectMany(track => track.Files.Select(_ => track.SourceId))];
        string[] paths = [.. tracks.SelectMany(track => track.Files.Select(file => file.RelativePath))];
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);

                // A file kept at a new place has another path (or source) now and is not deleted.
                await dbContext.Database.ExecuteSqlAsync(
                    $"""
                    DELETE FROM media_files
                    USING unnest({fileIds}, {fileSourceIds}, {paths}) AS gone(id, source_id, relative_path)
                    WHERE media_files.id = gone.id
                        AND media_files.source_id = gone.source_id
                        AND media_files.relative_path = gone.relative_path
                    """,
                    token);

                // A track kept at a new place has its files there, also when it stayed in its folder.
                int deleted = await dbContext.Database.ExecuteSqlAsync(
                    $"""
                    DELETE FROM tracks
                    USING unnest({trackIds}, {trackSourceIds}, {directories}) AS gone(id, source_id, directory_path)
                    WHERE tracks.id = gone.id
                        AND tracks.source_id = gone.source_id
                        AND tracks.directory_path = gone.directory_path
                        AND NOT EXISTS (SELECT 1 FROM media_files WHERE media_files.track_id = tracks.id)
                    """,
                    token);
                await transaction.CommitAsync(token);
                return deleted;
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<AlbumFolder>> GetAlbumFoldersAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Tracks.AsNoTracking()
            .Where(track => track.AlbumId != null)
            .Select(track => new AlbumFolder(track.AlbumId!.Value, track.SourceId, track.DirectoryPath))
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlbumCoverImage>> GetAlbumCoverImagesAsync(
        IReadOnlyCollection<string> normalizedBaseNames,
        CancellationToken cancellationToken = default)
    {
        string[] names = [.. normalizedBaseNames];
        return await dbContext.MediaFiles.AsNoTracking()
            .Where(file => file.Kind == MediaFileKind.Image && names.Contains(file.NormalizedBaseName))
            .Select(file => new AlbumCoverImage(
                file.Id,
                file.SourceId,
                file.RelativePath,
                file.DirectoryPath,
                file.NormalizedBaseName,
                file.Extension,
                file.Size,
                file.Width,
                file.Height))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Cover>> LoadAlbumCoversAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Covers
            .Where(cover => cover.AlbumId != null)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArtistFolder>> GetArtistFoldersAsync(
        CancellationToken cancellationToken = default)
    {
        List<ArtistFolder> asTrackArtist = await dbContext.Tracks.AsNoTracking()
            .Where(track => track.ArtistId != null)
            .Select(track => new ArtistFolder(
                track.ArtistId!.Value,
                track.Artist!.Name,
                track.SourceId,
                track.DirectoryPath))
            .Distinct()
            .ToListAsync(cancellationToken);
        List<ArtistFolder> asAlbumArtist = await dbContext.Tracks.AsNoTracking()
            .Where(track => track.Album != null && track.Album.AlbumArtistId != null)
            .Select(track => new ArtistFolder(
                track.Album!.AlbumArtistId!.Value,
                track.Album.AlbumArtist!.Name,
                track.SourceId,
                track.DirectoryPath))
            .Distinct()
            .ToListAsync(cancellationToken);
        return [.. asTrackArtist.Union(asAlbumArtist)];
    }

    public async Task<IReadOnlyList<ArtistPictureImage>> GetArtistPictureImagesAsync(
        IReadOnlyCollection<string> normalizedBaseNames,
        CancellationToken cancellationToken = default)
    {
        string[] names = [.. normalizedBaseNames];
        return await dbContext.MediaFiles.AsNoTracking()
            .Where(file => file.Kind == MediaFileKind.Image
                && file.Issue == null
                && names.Contains(file.NormalizedBaseName))
            .Select(file => new ArtistPictureImage(
                file.Id,
                file.SourceId,
                file.RelativePath,
                file.DirectoryPath,
                file.NormalizedBaseName,
                file.Size,
                file.Width,
                file.Height))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetArtistLocalPicturesAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Artists.AsNoTracking()
            .Where(artist => artist.LocalPictureFileId != null)
            .ToDictionaryAsync(artist => artist.Id, artist => artist.LocalPictureFileId!.Value, cancellationToken);
    }

    public async Task SetArtistLocalPicturesAsync(
        IReadOnlyDictionary<Guid, Guid?> pictures,
        CancellationToken cancellationToken = default)
    {
        if (pictures.Count == 0)
        {
            return;
        }

        Guid[] artistIds = [.. pictures.Keys];
        Guid?[] fileIds = [.. artistIds.Select(id => pictures[id])];

        // A file removed in the meantime is not set; the next scan finds the picture again.
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE artists
            SET local_picture_file_id = changed.file_id
            FROM unnest({artistIds}, {fileIds}) AS changed(artist_id, file_id)
            WHERE artists.id = changed.artist_id
                AND (changed.file_id IS NULL
                    OR EXISTS (SELECT 1 FROM media_files WHERE media_files.id = changed.file_id))
            """,
            cancellationToken);
    }

    public Task<(int Albums, int Artists)> DeleteOrphansAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                List<Guid> orphans = await dbContext.Albums
                    .Where(album => !dbContext.Tracks.Any(track => track.AlbumId == album.Id))
                    .OrderBy(album => album.Id)
                    .Select(album => album.Id)
                    .ToListAsync(token);
                int albums = 0;
                if (orphans.Count > 0)
                {
                    await CarryOverAsync(orphans, now, token);
                    albums = await dbContext.Albums
                        .Where(album => orphans.Contains(album.Id)
                            && !dbContext.Tracks.Any(track => track.AlbumId == album.Id))
                        .ExecuteDeleteAsync(token);
                }

                // After the albums, so an artist whose last album was just deleted is left empty as well.
                List<Guid> emptyArtists = await dbContext.Artists
                    .Where(artist => !dbContext.Tracks.Any(track => track.ArtistId == artist.Id)
                        && !dbContext.Albums.Any(album => album.AlbumArtistId == artist.Id))
                    .OrderBy(artist => artist.Id)
                    .Select(artist => artist.Id)
                    .ToListAsync(token);
                int artists = 0;
                if (emptyArtists.Count > 0)
                {
                    await CarryOverArtistsAsync(emptyArtists, token);
                    artists = await dbContext.Artists
                        .Where(artist => emptyArtists.Contains(artist.Id)
                            && !dbContext.Tracks.Any(track => track.ArtistId == artist.Id)
                            && !dbContext.Albums.Any(album => album.AlbumArtistId == artist.Id))
                        .ExecuteDeleteAsync(token);
                }

                await transaction.CommitAsync(token);
                return (albums, artists);
            },
            cancellationToken);
    }

    public async Task<int> UpdateAlbumCompilationsAsync(
        string compilationArtistKey,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // One statement for the whole library, since an album may span folders and sources. Only albums whose value
        // differs are written, so their change time stays as long as nothing changes.
        return await dbContext.Albums
            .Where(album => album.IsCompilation != (
                (album.AlbumArtist != null && album.AlbumArtist.NormalizedName == compilationArtistKey)
                || dbContext.MediaFiles.Any(file => file.IsPrimary
                    && file.Tags.IsCompilation
                    && file.Track!.AlbumId == album.Id
                    && (file.Kind == MediaFileKind.Audio || !file.Track.HasAudio))))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(album => album.IsCompilation, album => !album.IsCompilation)
                    .SetProperty(album => album.UpdatedAt, now),
                cancellationToken);
    }

    public async Task<LibraryTotals> CountLibraryAsync(CancellationToken cancellationToken = default)
    {
        return new LibraryTotals(
            await dbContext.Tracks.CountAsync(cancellationToken),
            await dbContext.Albums.CountAsync(cancellationToken),
            await dbContext.Artists.CountAsync(cancellationToken));
    }

    /// <summary>
    /// Gives the data of the albums left empty to the albums their songs moved to, by the plan of
    /// <see cref="AlbumSuccessionPlanner"/>. Runs in the transaction of <see cref="DeleteOrphansAsync"/>, before the
    /// albums left empty are deleted (their moves, favorites and online covers go with them).
    /// </summary>
    private async Task CarryOverAsync(List<Guid> orphans, DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<AlbumSuccessionMove> moves = await dbContext.AlbumMoves.AsNoTracking()
            .Where(move => orphans.Contains(move.FromAlbumId) || orphans.Contains(move.ToAlbumId))
            .Select(move => new AlbumSuccessionMove(move.FromAlbumId, move.ToAlbumId, move.TrackCount, move.MovedAt))
            .ToListAsync(cancellationToken);
        if (moves.Count == 0)
        {
            return;
        }

        Guid[] ids = [.. moves.SelectMany(move => new[] { move.FromAlbumId, move.ToAlbumId }).Union(orphans)];
        List<AlbumSuccessionAlbum> albums = await dbContext.Albums.AsNoTracking()
            .Where(album => ids.Contains(album.Id))
            .Select(album => new AlbumSuccessionAlbum(
                album.Id,
                dbContext.Tracks.Any(track => track.AlbumId == album.Id),
                album.FirstSeenAt,
                album.CoverPriority,
                dbContext.OnlineCovers
                    .Where(cover => cover.AlbumId == album.Id)
                    .Select(cover => (Guid?)cover.Id)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
        List<AlbumSuccessionFavorite> favorites = await dbContext.Favorites.AsNoTracking()
            .Where(favorite => favorite.AlbumId != null && orphans.Contains(favorite.AlbumId.Value))
            .Select(favorite =>
                new AlbumSuccessionFavorite(favorite.UserId, favorite.AlbumId!.Value, favorite.CreatedAt))
            .ToListAsync(cancellationToken);
        AlbumSuccessionPlan plan = AlbumSuccessionPlanner.Plan(albums, moves, favorites);
        if (plan.Successors.Count == 0)
        {
            return;
        }

        if (plan.Favorites.Count > 0)
        {
            Guid[] favoriteIds = [.. plan.Favorites.Select(_ => Guid.CreateVersion7())];
            Guid[] userIds = [.. plan.Favorites.Select(favorite => favorite.UserId)];
            Guid[] albumIds = [.. plan.Favorites.Select(favorite => favorite.AlbumId)];
            DateTimeOffset[] createdAt = [.. plan.Favorites.Select(favorite => favorite.CreatedAt)];
            string type = nameof(FavoriteType.Album);

            // A profile that already marked the successor keeps its favorite, with the earlier time.
            await dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO favorites (id, user_id, type, album_id, created_at)
                SELECT carried.id, carried.user_id, {type}, carried.album_id, carried.created_at
                FROM unnest({favoriteIds}, {userIds}, {albumIds}, {createdAt})
                    AS carried(id, user_id, album_id, created_at)
                ON CONFLICT (user_id, album_id) WHERE album_id IS NOT NULL
                DO UPDATE SET created_at = LEAST(favorites.created_at, excluded.created_at)
                """,
                cancellationToken);
        }

        if (plan.Updates.Count > 0)
        {
            Guid[] albumIds = [.. plan.Updates.Select(update => update.AlbumId)];
            DateTimeOffset[] firstSeenAt = [.. plan.Updates.Select(update => update.FirstSeenAt)];
            string?[] coverPriorities = [.. plan.Updates.Select(update => update.CoverPriority?.ToString())];
            await dbContext.Database.ExecuteSqlAsync(
                $"""
                UPDATE albums
                SET first_seen_at = changed.first_seen_at, cover_priority = changed.cover_priority, updated_at = {now}
                FROM unnest({albumIds}, {firstSeenAt}, {coverPriorities})
                    AS changed(album_id, first_seen_at, cover_priority)
                WHERE albums.id = changed.album_id
                """,
                cancellationToken);
        }

        foreach (AlbumSuccessionOnlineCover onlineCover in plan.OnlineCovers)
        {
            await dbContext.OnlineCovers
                .Where(cover => cover.Id == onlineCover.OnlineCoverId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(cover => cover.AlbumId, onlineCover.AlbumId),
                    cancellationToken);
        }

        // A list in the player started from an album left empty continues with its main successor.
        Guid[] orphanIds = [.. plan.Successors.Keys];
        Guid[] mainSuccessorIds = [.. orphanIds.Select(orphan => plan.Successors[orphan][0])];
        string albumKind = nameof(PlaybackSourceKind.Album);
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE playback_states
            SET source_id = successor.main_successor_id
            FROM unnest({orphanIds}, {mainSuccessorIds}) AS successor(album_id, main_successor_id)
            WHERE playback_states.source_kind = {albumKind} AND playback_states.source_id = successor.album_id
            """,
            cancellationToken);

        if (plan.PassedOnMoves.Count > 0)
        {
            Guid[] fromAlbumIds = [.. plan.PassedOnMoves.Select(move => move.FromAlbumId)];
            Guid[] toAlbumIds = [.. plan.PassedOnMoves.Select(move => move.ToAlbumId)];
            int[] trackCounts = [.. plan.PassedOnMoves.Select(move => move.TrackCount)];
            DateTimeOffset[] movedAt = [.. plan.PassedOnMoves.Select(move => move.MovedAt)];
            await dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO album_moves (from_album_id, to_album_id, track_count, moved_at)
                SELECT * FROM unnest({fromAlbumIds}, {toAlbumIds}, {trackCounts}, {movedAt})
                ON CONFLICT (from_album_id, to_album_id)
                DO UPDATE SET track_count = album_moves.track_count + excluded.track_count,
                    moved_at = GREATEST(album_moves.moved_at, excluded.moved_at)
                """,
                cancellationToken);
        }
    }

    /// <summary>
    /// Gives the data of the artists left empty to the artists their songs moved to, by the plan of
    /// <see cref="ArtistSuccessionPlanner"/>. Runs in the transaction of <see cref="DeleteOrphansAsync"/>, after the
    /// albums left empty are deleted and before the artists left empty are deleted (their moves, favorites and the
    /// pictures not passed on go with them). Artists have no change time, so nothing else is touched.
    /// </summary>
    private async Task CarryOverArtistsAsync(List<Guid> orphans, CancellationToken cancellationToken)
    {
        List<ArtistSuccessionMove> moves = await dbContext.ArtistMoves.AsNoTracking()
            .Where(move => orphans.Contains(move.FromArtistId) || orphans.Contains(move.ToArtistId))
            .Select(move =>
                new ArtistSuccessionMove(move.FromArtistId, move.ToArtistId, move.TrackCount, move.MovedAt))
            .ToListAsync(cancellationToken);
        if (moves.Count == 0)
        {
            return;
        }

        Guid[] ids = [.. moves.SelectMany(move => new[] { move.FromArtistId, move.ToArtistId }).Union(orphans)];
        List<ArtistSuccessionArtist> artists = await dbContext.Artists.AsNoTracking()
            .Where(artist => ids.Contains(artist.Id))
            .Select(artist => new ArtistSuccessionArtist(
                artist.Id,
                dbContext.Tracks.Any(track => track.ArtistId == artist.Id)
                    || dbContext.Albums.Any(album => album.AlbumArtistId == artist.Id),
                artist.FirstSeenAt,
                dbContext.ArtistPictures
                    .Where(picture => picture.ArtistId == artist.Id && picture.Kind == ArtistPictureKind.Uploaded)
                    .Select(picture => (Guid?)picture.Id)
                    .FirstOrDefault(),
                dbContext.ArtistPictures
                    .Where(picture => picture.ArtistId == artist.Id && picture.Kind == ArtistPictureKind.Online)
                    .Select(picture => (Guid?)picture.Id)
                    .FirstOrDefault(),
                artist.MusicBrainzArtistId))
            .ToListAsync(cancellationToken);
        List<ArtistSuccessionFavorite> favorites = await dbContext.Favorites.AsNoTracking()
            .Where(favorite => favorite.ArtistId != null && orphans.Contains(favorite.ArtistId.Value))
            .Select(favorite =>
                new ArtistSuccessionFavorite(favorite.UserId, favorite.ArtistId!.Value, favorite.CreatedAt))
            .ToListAsync(cancellationToken);
        ArtistSuccessionPlan plan = ArtistSuccessionPlanner.Plan(artists, moves, favorites);
        if (plan.Successors.Count == 0)
        {
            return;
        }

        if (plan.Favorites.Count > 0)
        {
            Guid[] favoriteIds = [.. plan.Favorites.Select(_ => Guid.CreateVersion7())];
            Guid[] userIds = [.. plan.Favorites.Select(favorite => favorite.UserId)];
            Guid[] artistIds = [.. plan.Favorites.Select(favorite => favorite.ArtistId)];
            DateTimeOffset[] createdAt = [.. plan.Favorites.Select(favorite => favorite.CreatedAt)];
            string type = nameof(FavoriteType.Artist);

            // A profile that already marked the successor keeps its favorite, with the earlier time.
            await dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO favorites (id, user_id, type, artist_id, created_at)
                SELECT carried.id, carried.user_id, {type}, carried.artist_id, carried.created_at
                FROM unnest({favoriteIds}, {userIds}, {artistIds}, {createdAt})
                    AS carried(id, user_id, artist_id, created_at)
                ON CONFLICT (user_id, artist_id) WHERE artist_id IS NOT NULL
                DO UPDATE SET created_at = LEAST(favorites.created_at, excluded.created_at)
                """,
                cancellationToken);
        }

        if (plan.Updates.Count > 0)
        {
            Guid[] artistIds = [.. plan.Updates.Select(update => update.ArtistId)];
            DateTimeOffset[] firstSeenAt = [.. plan.Updates.Select(update => update.FirstSeenAt)];
            Guid?[] musicBrainzArtistIds = [.. plan.Updates.Select(update => update.MusicBrainzArtistId)];
            await dbContext.Database.ExecuteSqlAsync(
                $"""
                UPDATE artists
                SET first_seen_at = changed.first_seen_at, music_brainz_artist_id = changed.music_brainz_artist_id
                FROM unnest({artistIds}, {firstSeenAt}, {musicBrainzArtistIds})
                    AS changed(artist_id, first_seen_at, music_brainz_artist_id)
                WHERE artists.id = changed.artist_id
                """,
                cancellationToken);
        }

        // The main successor has no picture of the kind (one per artist and kind), so the unique index holds.
        if (plan.Pictures.Count > 0)
        {
            Guid[] pictureIds = [.. plan.Pictures.Select(picture => picture.PictureId)];
            Guid[] artistIds = [.. plan.Pictures.Select(picture => picture.ArtistId)];
            await dbContext.Database.ExecuteSqlAsync(
                $"""
                UPDATE artist_pictures
                SET artist_id = moved.artist_id
                FROM unnest({pictureIds}, {artistIds}) AS moved(picture_id, artist_id)
                WHERE artist_pictures.id = moved.picture_id
                """,
                cancellationToken);
        }

        // A list in the player started from an artist left empty continues with its main successor.
        Guid[] orphanIds = [.. plan.Successors.Keys];
        Guid[] mainSuccessorIds = [.. orphanIds.Select(orphan => plan.Successors[orphan][0])];
        string artistKind = nameof(PlaybackSourceKind.Artist);
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE playback_states
            SET source_id = successor.main_successor_id
            FROM unnest({orphanIds}, {mainSuccessorIds}) AS successor(artist_id, main_successor_id)
            WHERE playback_states.source_kind = {artistKind} AND playback_states.source_id = successor.artist_id
            """,
            cancellationToken);

        if (plan.PassedOnMoves.Count > 0)
        {
            Guid[] fromArtistIds = [.. plan.PassedOnMoves.Select(move => move.FromArtistId)];
            Guid[] toArtistIds = [.. plan.PassedOnMoves.Select(move => move.ToArtistId)];
            int[] trackCounts = [.. plan.PassedOnMoves.Select(move => move.TrackCount)];
            DateTimeOffset[] movedAt = [.. plan.PassedOnMoves.Select(move => move.MovedAt)];
            await dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO artist_moves (from_artist_id, to_artist_id, track_count, moved_at)
                SELECT * FROM unnest({fromArtistIds}, {toArtistIds}, {trackCounts}, {movedAt})
                ON CONFLICT (from_artist_id, to_artist_id)
                DO UPDATE SET track_count = artist_moves.track_count + excluded.track_count,
                    moved_at = GREATEST(artist_moves.moved_at, excluded.moved_at)
                """,
                cancellationToken);
        }
    }

    /// <summary>
    /// Records a move for every song of the deleted folders whose file lies elsewhere in the library now, so the album
    /// of a renamed or moved folder (an album without artist is kept apart per folder) gives its data to the album of
    /// the new folder once it is left empty, and likewise an artist (an artist taken from the folder name, say) to the
    /// artist of the song found elsewhere. A song counts as moved when one of its primary files has the same kind, file
    /// name and size as a primary file of a song outside the deleted folders. For artists, the track artist and the
    /// album artist of its album are both roles of a song, as in the writer: a role that changed from one artist to
    /// another counts only when the old artist has neither role on the song found elsewhere, and a pair counts once per
    /// song. There is no time condition, so a scan that is repeated after an interruption still finds the folder
    /// written by the interrupted run. The count of a pair is the number of songs of the deleted folders; it is added
    /// to a stored move of the pair.
    /// </summary>
    private async Task RecordFolderMovesAsync(
        Guid sourceId,
        string[] paths,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO album_moves (from_album_id, to_album_id, track_count, moved_at)
            SELECT old_track.album_id, new_track.album_id, count(DISTINCT old_track.id), {now}
            FROM tracks AS old_track
            JOIN media_files AS old_file ON old_file.track_id = old_track.id AND old_file.is_primary
            JOIN media_files AS new_file ON new_file.is_primary
                AND new_file.kind = old_file.kind
                AND new_file.file_name = old_file.file_name
                AND new_file.size = old_file.size
            JOIN tracks AS new_track ON new_track.id = new_file.track_id
            WHERE old_track.source_id = {sourceId}
                AND old_track.directory_path = ANY({paths})
                AND old_track.album_id IS NOT NULL
                AND new_track.album_id IS NOT NULL
                AND new_track.album_id <> old_track.album_id
                AND NOT (new_track.source_id = {sourceId} AND new_track.directory_path = ANY({paths}))
            GROUP BY old_track.album_id, new_track.album_id
            ON CONFLICT (from_album_id, to_album_id)
            DO UPDATE SET track_count = album_moves.track_count + excluded.track_count, moved_at = excluded.moved_at
            """,
            cancellationToken);

        // The new artist of a role is one of the two artists of the song found elsewhere, so a pair whose old artist
        // differs from both never names the same artist twice.
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO artist_moves (from_artist_id, to_artist_id, track_count, moved_at)
            SELECT role.from_artist_id, role.to_artist_id, count(DISTINCT old_track.id), {now}
            FROM tracks AS old_track
            LEFT JOIN albums AS old_album ON old_album.id = old_track.album_id
            JOIN media_files AS old_file ON old_file.track_id = old_track.id AND old_file.is_primary
            JOIN media_files AS new_file ON new_file.is_primary
                AND new_file.kind = old_file.kind
                AND new_file.file_name = old_file.file_name
                AND new_file.size = old_file.size
            JOIN tracks AS new_track ON new_track.id = new_file.track_id
            LEFT JOIN albums AS new_album ON new_album.id = new_track.album_id
            CROSS JOIN LATERAL (VALUES
                (old_track.artist_id, new_track.artist_id),
                (old_album.album_artist_id, new_album.album_artist_id)) AS role(from_artist_id, to_artist_id)
            WHERE old_track.source_id = {sourceId}
                AND old_track.directory_path = ANY({paths})
                AND NOT (new_track.source_id = {sourceId} AND new_track.directory_path = ANY({paths}))
                AND role.from_artist_id IS NOT NULL
                AND role.to_artist_id IS NOT NULL
                AND role.from_artist_id IS DISTINCT FROM new_track.artist_id
                AND role.from_artist_id IS DISTINCT FROM new_album.album_artist_id
            GROUP BY role.from_artist_id, role.to_artist_id
            ON CONFLICT (from_artist_id, to_artist_id)
            DO UPDATE SET track_count = artist_moves.track_count + excluded.track_count, moved_at = excluded.moved_at
            """,
            cancellationToken);
    }

    private static IQueryable<MediaFileSnapshot> ToSnapshots(IQueryable<MediaFile> files)
    {
        return files.Select(file => new MediaFileSnapshot(
            file.Id,
            file.RelativePath,
            file.DirectoryPath,
            file.Kind,
            file.Size,
            file.ModifiedAt,
            file.MetadataVersion,
            file.TrackId,
            file.IsPrimary));
    }
}
