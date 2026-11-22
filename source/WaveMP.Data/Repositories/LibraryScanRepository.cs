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

                int artists = await dbContext.Artists
                    .Where(artist => !dbContext.Tracks.Any(track => track.ArtistId == artist.Id)
                        && !dbContext.Albums.Any(album => album.AlbumArtistId == artist.Id))
                    .ExecuteDeleteAsync(token);
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
    /// Records a move for every song of the deleted folders whose file lies elsewhere in the library now, so the album
    /// of a renamed or moved folder (an album without artist is kept apart per folder) gives its data to the album of
    /// the new folder once it is left empty. A song counts as moved when one of its primary files has the same kind,
    /// file name and size as a primary file of a song outside the deleted folders. There is no time condition, so a
    /// scan that is repeated after an interruption still finds the folder written by the interrupted run. The count of
    /// a pair is the number of songs of the deleted folders; it is added to a stored move of the pair.
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
            file.MetadataVersion));
    }
}
