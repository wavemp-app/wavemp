using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

internal sealed class OrganizeRepository(WaveMpDbContext dbContext) : IOrganizeRepository
{
    public async Task<IReadOnlyList<TemplateSongRow>> GetTemplateSongsAsync(
        IReadOnlyList<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        if (trackIds.Count == 0)
        {
            return [];
        }

        Guid[] ids = [.. trackIds.Distinct()];
        IReadOnlyList<TemplateSongRow> rows = await LoadAsync(
            dbContext.Tracks.AsNoTracking().Where(track => ids.Contains(track.Id)),
            cancellationToken);
        var byId = rows.ToDictionary(row => row.TrackId);
        return [.. ids.Where(byId.ContainsKey).Select(id => byId[id])];
    }

    public async Task<IReadOnlyList<TemplateSongRow>> GetSampleTemplateSongsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Track> tracks = dbContext.Tracks.AsNoTracking()
            .OrderBy(track => track.AlbumId == null)
            .ThenBy(track => track.Album!.AlbumArtist!.NormalizedName)
            .ThenBy(track => track.Album!.NormalizedTitle)
            .ThenBy(track => track.DiscNumber)
            .ThenBy(track => track.TrackNumber)
            .ThenBy(track => track.Title)
            .ThenBy(track => track.Id)
            .Take(count);
        return await LoadAsync(tracks, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizeSongRow>> GetSongsAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        if (trackIds.Count == 0)
        {
            return [];
        }

        Guid[] ids = [.. trackIds.Distinct()];
        var songs = await dbContext.Tracks.AsNoTracking()
            .Where(track => ids.Contains(track.Id))
            .Select(track => new
            {
                track.Id,
                track.SourceId,
                track.DirectoryPath,
                track.BaseName,
                track.Title,
                ArtistName = track.Artist == null ? null : track.Artist.Name,
                track.AlbumId,
                AlbumTitle = track.Album == null ? null : track.Album.Title,
                AlbumArtistName = track.Album == null || track.Album.AlbumArtist == null
                    ? null
                    : track.Album.AlbumArtist.Name,
                IsCompilation = track.Album != null && track.Album.IsCompilation,
                Year = track.Year ?? (track.Album == null ? null : track.Album.Year),
                track.TrackNumber,
                track.DiscNumber,
                Genre = track.Genre == null ? null : track.Genre.Name,
            })
            .ToListAsync(cancellationToken);
        ILookup<Guid, OrganizeFileRow> files = (await LoadFilesAsync(
                dbContext.MediaFiles.Where(file => file.TrackId != null && ids.Contains(file.TrackId.Value)),
                cancellationToken))
            .ToLookup(file => file.TrackId!.Value, file => file.Row);

        return [.. songs.Select(song => new OrganizeSongRow(
            song.Id,
            song.SourceId,
            song.DirectoryPath,
            song.BaseName,
            song.Title,
            song.ArtistName,
            song.AlbumId,
            song.AlbumTitle,
            song.AlbumArtistName,
            song.IsCompilation,
            song.Year,
            song.TrackNumber,
            song.DiscNumber,
            song.Genre,
            [.. files[song.Id].OrderBy(file => file.RelativePath, StringComparer.Ordinal)]))];
    }

    public async Task<IReadOnlyDictionary<Guid, OrganizeFileRow>> GetFilesAsync(
        IReadOnlyCollection<Guid> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        if (mediaFileIds.Count == 0)
        {
            return new Dictionary<Guid, OrganizeFileRow>();
        }

        Guid[] ids = [.. mediaFileIds.Distinct()];
        IReadOnlyList<(Guid? TrackId, string DirectoryPath, OrganizeFileRow Row)> files = await LoadFilesAsync(
            dbContext.MediaFiles.Where(file => ids.Contains(file.Id)),
            cancellationToken);
        return files.ToDictionary(file => file.Row.MediaFileId, file => file.Row);
    }

    public async Task<IReadOnlyList<Guid>> ListTrackIdsAsync(
        Guid? sourceId,
        Guid? afterId,
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Track> tracks = dbContext.Tracks.AsNoTracking();
        if (sourceId is { } source)
        {
            tracks = tracks.Where(track => track.SourceId == source);
        }

        if (afterId is { } after)
        {
            tracks = tracks.Where(track => track.Id.CompareTo(after) > 0);
        }

        return await tracks.OrderBy(track => track.Id).Select(track => track.Id).Take(count).ToListAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizeFolderRow>> GetFoldersAsync(
        Guid sourceId,
        IReadOnlyCollection<string> directoryPaths,
        CancellationToken cancellationToken = default)
    {
        if (directoryPaths.Count == 0)
        {
            return [];
        }

        string[] paths = [.. directoryPaths.Distinct(StringComparer.Ordinal)];
        ILookup<string, Guid> tracks = (await dbContext.Tracks.AsNoTracking()
                .Where(track => track.SourceId == sourceId && paths.Contains(track.DirectoryPath))
                .Select(track => new { track.DirectoryPath, track.Id })
                .ToListAsync(cancellationToken))
            .ToLookup(track => track.DirectoryPath, track => track.Id, StringComparer.Ordinal);
        ILookup<string, OrganizeFileRow> loose = (await LoadFilesAsync(
                dbContext.MediaFiles.Where(file => file.SourceId == sourceId
                    && file.TrackId == null
                    && paths.Contains(file.DirectoryPath)),
                cancellationToken))
            .ToLookup(file => file.DirectoryPath, file => file.Row, StringComparer.Ordinal);
        return [.. paths.Select(path => new OrganizeFolderRow(
            sourceId,
            path,
            [.. tracks[path]],
            [.. loose[path].OrderBy(file => file.RelativePath, StringComparer.Ordinal)]))];
    }

    public Task<int> CountOtherAlbumTracksAsync(
        string albumKey,
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        Guid[] ids = [.. trackIds];
        return dbContext.Tracks.AsNoTracking()
            .CountAsync(
                track => track.Album != null && track.Album.AlbumKey == albumKey && !ids.Contains(track.Id),
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, TagBackup>> GetTagBackupsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, TagBackup>();
        }

        Guid[] keys = [.. ids.Distinct()];
        return await dbContext.TagBackups.AsNoTracking()
            .Where(backup => keys.Contains(backup.Id))
            .ToDictionaryAsync(backup => backup.Id, cancellationToken);
    }

    public async Task AddOperationAsync(OrganizeOperation operation, CancellationToken cancellationToken = default)
    {
        dbContext.OrganizeOperations.Add(operation);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    public async Task AddItemsAsync(IReadOnlyList<OrganizeItem> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        dbContext.OrganizeItems.AddRange(items);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();

        // The progress of the planning.
        foreach (IGrouping<Guid, OrganizeItem> operation in items.GroupBy(item => item.OperationId))
        {
            int count = operation.Count();
            await dbContext.OrganizeOperations
                .Where(row => row.Id == operation.Key)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(row => row.TotalItems, row => row.TotalItems + count),
                    cancellationToken);
        }
    }

    public async Task<bool> FinishPlanningAsync(
        Guid id,
        OrganizeOperationStatus status,
        OrganizeErrorCode? errorCode,
        DateTimeOffset previewedAt,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
    {
        IQueryable<OrganizeItem> items = dbContext.OrganizeItems.Where(item => item.OperationId == id);
        int total = await items.CountAsync(cancellationToken);
        int conflicts = await items.CountAsync(item => item.Conflict != null, cancellationToken);
        int warnings = await items.CountAsync(item => item.Warnings.Count > 0, cancellationToken);
        int changed = await dbContext.OrganizeOperations
            .Where(operation => operation.Id == id && operation.Status == OrganizeOperationStatus.Planning)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(operation => operation.Status, status)
                    .SetProperty(operation => operation.ErrorCode, errorCode)
                    .SetProperty(operation => operation.TotalItems, total)
                    .SetProperty(operation => operation.ConflictItems, conflicts)
                    .SetProperty(operation => operation.WarningItems, warnings)
                    .SetProperty(operation => operation.PreviewedAt, previewedAt)
                    .SetProperty(operation => operation.ExpiresAt, expiresAt),
                cancellationToken);
        return changed > 0;
    }

    public Task<OrganizeOperation?> FindOperationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.OrganizeOperations.AsNoTracking().SingleOrDefaultAsync(
            operation => operation.Id == id,
            cancellationToken);
    }

    public Task<Page<OrganizeOperation>> ListOperationsAsync(
        OrganizeOperationListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<OrganizeOperation> operations = dbContext.OrganizeOperations.AsNoTracking();
        if (query.Kind is { } kind)
        {
            operations = operations.Where(operation => operation.Kind == kind);
        }

        if (query.Status is { } status)
        {
            operations = operations.Where(operation => operation.Status == status);
        }

        return operations
            .OrderByDescending(operation => operation.CreatedAt)
            .ThenByDescending(operation => operation.Id)
            .ToPageAsync(query.Offset, query.Limit, cancellationToken);
    }

    public Task<Page<OrganizeItem>> ListItemsAsync(
        OrganizeItemListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<OrganizeItem> items = dbContext.OrganizeItems.AsNoTracking()
            .Where(item => item.OperationId == query.OperationId);
        items = query.Filter switch
        {
            OrganizeItemFilter.Conflicts => items.Where(item => item.Conflict != null),
            OrganizeItemFilter.Warnings => items.Where(item => item.Warnings.Count > 0),
            _ => items,
        };
        return items
            .Include(item => item.Moves.OrderBy(move => move.Sequence))
            .OrderBy(item => item.Sequence)
            .ThenBy(item => item.Id)
            .AsSplitQuery()
            .ToPageAsync(query.Offset, query.Limit, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizeItem>> ListItemsByStatusAsync(
        Guid operationId,
        OrganizeItemStatus status,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizeItems.AsNoTracking()
            .Where(item => item.OperationId == operationId && item.Status == status)
            .Include(item => item.Moves.OrderBy(move => move.Sequence))
            .OrderBy(item => item.Sequence)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public Task<Guid?> FindActiveOperationIdAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.OrganizeOperations.AsNoTracking()
            .Where(operation => operation.Status == OrganizeOperationStatus.Queued
                || operation.Status == OrganizeOperationStatus.Running)
            .OrderBy(operation => operation.CreatedAt)
            .Select(operation => (Guid?)operation.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> ExpirePreviewsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        return dbContext.OrganizeOperations
            .Where(operation => operation.Status == OrganizeOperationStatus.Previewed
                && operation.ExpiresAt != null
                && operation.ExpiresAt < now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(operation => operation.Status, OrganizeOperationStatus.Expired),
                cancellationToken);
    }

    public async Task<OrganizeDiscardStatus> DeletePreviewAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        int deleted = await dbContext.OrganizeOperations
            .Where(operation => operation.Id == id
                && (operation.Status == OrganizeOperationStatus.Planning
                    || operation.Status == OrganizeOperationStatus.Previewed
                    || operation.Status == OrganizeOperationStatus.Expired))
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
        {
            return OrganizeDiscardStatus.Discarded;
        }

        return await dbContext.OrganizeOperations.AnyAsync(operation => operation.Id == id, cancellationToken)
            ? OrganizeDiscardStatus.NotPreview
            : OrganizeDiscardStatus.NotFound;
    }

    public Task RelocateTrackAsync(TrackRelocation relocation, CancellationToken cancellationToken = default)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await RelocateInTransactionAsync(relocation, token);
                await transaction.CommitAsync(token);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListItemSourceIdsAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizeItems.AsNoTracking()
            .Where(item => item.OperationId == operationId
                && (item.Status == OrganizeItemStatus.Pending || item.Status == OrganizeItemStatus.Conflict))
            .Select(item => item.SourceId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> QueueAsync(Guid id, bool skipConflicts, CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizeOperations
            .Where(operation => operation.Id == id && operation.Status == OrganizeOperationStatus.Previewed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(operation => operation.Status, OrganizeOperationStatus.Queued)
                    .SetProperty(operation => operation.SkipConflicts, skipConflicts),
                cancellationToken) > 0;
    }

    public async Task<bool> UnqueueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizeOperations
            .Where(operation => operation.Id == id && operation.Status == OrganizeOperationStatus.Queued)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(operation => operation.Status, OrganizeOperationStatus.Previewed)
                    .SetProperty(operation => operation.SkipConflicts, false)
                    .SetProperty(operation => operation.CancelRequestedAt, (DateTimeOffset?)null),
                cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<Guid>> ListOperationIdsAsync(
        OrganizeOperationStatus status,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizeOperations.AsNoTracking()
            .Where(operation => operation.Status == status)
            .OrderBy(operation => operation.CreatedAt)
            .ThenBy(operation => operation.Id)
            .Select(operation => operation.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> StartRunAsync(
        Guid id,
        DateTimeOffset startedAt,
        Guid? jobRunId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizeOperations
            .Where(operation => operation.Id == id && operation.Status == OrganizeOperationStatus.Queued)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(operation => operation.Status, OrganizeOperationStatus.Running)
                    .SetProperty(operation => operation.StartedAt, startedAt)
                    .SetProperty(operation => operation.JobRunId, jobRunId),
                cancellationToken) > 0;
    }

    public async Task FinishRunAsync(
        Guid id,
        OrganizeOperationStatus status,
        OrganizeErrorCode? errorCode,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        (int done, int skipped, int failed) = await CountRunAsync(id, cancellationToken);
        await dbContext.OrganizeOperations
            .Where(operation => operation.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(operation => operation.Status, status)
                    .SetProperty(operation => operation.ErrorCode, errorCode)
                    .SetProperty(operation => operation.FinishedAt, finishedAt)
                    .SetProperty(operation => operation.DoneItems, done)
                    .SetProperty(operation => operation.SkippedItems, skipped)
                    .SetProperty(operation => operation.FailedItems, failed),
                cancellationToken);
    }

    public async Task<OrganizeCancelStatus> RequestCancelAsync(
        Guid id,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        int changed = await dbContext.OrganizeOperations
            .Where(operation => operation.Id == id
                && (operation.Status == OrganizeOperationStatus.Planning
                    || operation.Status == OrganizeOperationStatus.Queued
                    || operation.Status == OrganizeOperationStatus.Running))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    operation => operation.CancelRequestedAt,
                    operation => operation.CancelRequestedAt ?? requestedAt),
                cancellationToken);
        if (changed > 0)
        {
            return OrganizeCancelStatus.Requested;
        }

        return await dbContext.OrganizeOperations.AnyAsync(operation => operation.Id == id, cancellationToken)
            ? OrganizeCancelStatus.NotCancelable
            : OrganizeCancelStatus.NotFound;
    }

    public async Task<IReadOnlyList<OrganizeItem>> ListItemsAfterAsync(
        Guid operationId,
        int afterSequence,
        int count,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizeItems.AsNoTracking()
            .Where(item => item.OperationId == operationId && item.Sequence > afterSequence)
            .Include(item => item.Moves.OrderBy(move => move.Sequence))
            .OrderBy(item => item.Sequence)
            .Take(count)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public Task<int> SkipConflictItemsAsync(
        Guid operationId,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        return dbContext.OrganizeItems
            .Where(item => item.OperationId == operationId && item.Status == OrganizeItemStatus.Conflict)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, OrganizeItemStatus.Skipped)
                    .SetProperty(item => item.FinishedAt, finishedAt),
                cancellationToken);
    }

    public async Task BeginItemAsync(
        Guid itemId,
        IReadOnlyList<string> createdFolders,
        CancellationToken cancellationToken = default)
    {
        List<string> folders = [.. createdFolders];
        await dbContext.OrganizeItems
            .Where(item => item.Id == itemId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, OrganizeItemStatus.Running)
                    .SetProperty(item => item.CreatedFolders, folders),
                cancellationToken);
    }

    public Task AddItemTagBackupAsync(
        Guid itemId,
        TagBackup backup,
        Guid? ownAlbumId,
        Guid? trackId,
        CancellationToken cancellationToken = default)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                dbContext.TagBackups.Add(backup);
                await dbContext.SaveChangesAsync(token);
                await dbContext.OrganizeItems
                    .Where(item => item.Id == itemId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.TagBackupId, backup.Id), token);
                if (ownAlbumId is { } albumId && trackId is { } track)
                {
                    await dbContext.OwnAlbumTracks
                        .Where(member => member.OwnAlbumId == albumId
                            && member.TrackId == track
                            && member.State == OwnAlbumTrackState.Member
                            && member.OriginalTagBackupId == null)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(member => member.OriginalTagBackupId, backup.Id),
                            token);
                }

                await transaction.CommitAsync(token);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }

    public Task UpdateMoveFingerprintAsync(
        Guid moveId,
        long size,
        DateTimeOffset modifiedAt,
        CancellationToken cancellationToken = default)
    {
        return dbContext.OrganizeFileMoves
            .Where(move => move.Id == moveId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(move => move.Size, size)
                    .SetProperty(move => move.ModifiedAt, modifiedAt),
                cancellationToken);
    }

    public async Task EndItemAsync(
        Guid itemId,
        OrganizeItemStatus status,
        OrganizeConflict? conflict,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        IQueryable<OrganizeItem> items = dbContext.OrganizeItems.Where(item => item.Id == itemId);
        if (conflict is { } code)
        {
            await items.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, status)
                    .SetProperty(item => item.Conflict, code)
                    .SetProperty(item => item.FinishedAt, finishedAt),
                cancellationToken);
        }
        else
        {
            await items.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, status)
                    .SetProperty(item => item.FinishedAt, finishedAt),
                cancellationToken);
        }

        if (status == OrganizeItemStatus.RolledBack)
        {
            await dbContext.OrganizeFileMoves
                .Where(move => move.ItemId == itemId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(move => move.Status, OrganizeMoveStatus.RolledBack),
                    cancellationToken);
        }
    }

    public async Task SetRemovedFoldersAsync(
        Guid itemId,
        IReadOnlyList<string> removedFolders,
        CancellationToken cancellationToken = default)
    {
        List<string> folders = [.. removedFolders];
        await dbContext.OrganizeItems
            .Where(item => item.Id == itemId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.RemovedFolders, folders),
                cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizeItem>> ListRunningItemsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.OrganizeItems.AsNoTracking()
            .Where(item => item.Status == OrganizeItemStatus.Running)
            .Include(item => item.Moves.OrderBy(move => move.Sequence))
            .OrderBy(item => item.OperationId)
            .ThenBy(item => item.Sequence)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> InterruptActiveAsync(
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        List<Guid> ids = await dbContext.OrganizeOperations.AsNoTracking()
            .Where(operation => operation.Status == OrganizeOperationStatus.Planning
                || operation.Status == OrganizeOperationStatus.Queued
                || operation.Status == OrganizeOperationStatus.Running)
            .Select(operation => operation.Id)
            .ToListAsync(cancellationToken);
        foreach (Guid id in ids)
        {
            await FinishRunAsync(
                id,
                OrganizeOperationStatus.Interrupted,
                OrganizeErrorCode.Interrupted,
                finishedAt,
                cancellationToken);
        }

        return ids;
    }

    public Task SetUndoneByAsync(Guid id, Guid undoId, CancellationToken cancellationToken = default)
    {
        return dbContext.OrganizeOperations
            .Where(operation => operation.Id == id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(operation => operation.UndoneById, undoId),
                cancellationToken);
    }

    public async Task UpdateOwnAlbumAppliedAtAsync(Guid ownAlbumId, CancellationToken cancellationToken = default)
    {
        DateTimeOffset? appliedAt = await dbContext.OrganizeOperations.AsNoTracking()
            .Where(operation => operation.OwnAlbumId == ownAlbumId
                && operation.Kind == OrganizeOperationKind.OwnAlbum
                && operation.UndoneById == null
                && operation.FinishedAt != null
                && (operation.Status == OrganizeOperationStatus.Completed
                    || operation.Status == OrganizeOperationStatus.Stopped
                    || operation.Status == OrganizeOperationStatus.Canceled
                    || operation.Status == OrganizeOperationStatus.Interrupted)
                && (operation.Status == OrganizeOperationStatus.Completed || operation.DoneItems > 0))
            .MaxAsync(operation => operation.FinishedAt, cancellationToken);
        await dbContext.OwnAlbums
            .Where(album => album.Id == ownAlbumId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(album => album.AppliedAt, appliedAt), cancellationToken);
    }

    public Task<int> DeleteOldPreviewsAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default)
    {
        return dbContext.OrganizeOperations
            .Where(operation => operation.CreatedAt < createdBefore
                && (operation.Status == OrganizeOperationStatus.Previewed
                    || operation.Status == OrganizeOperationStatus.Expired
                    || (operation.Status == OrganizeOperationStatus.Failed && operation.StartedAt == null)))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<(int Done, int Skipped, int Failed)> CountRunAsync(Guid id, CancellationToken cancellationToken)
    {
        var counts = await dbContext.OrganizeItems.AsNoTracking()
            .Where(item => item.OperationId == id)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        int Count(params OrganizeItemStatus[] statuses) =>
            counts.Where(row => statuses.Contains(row.Status)).Sum(row => row.Count);
        return (
            Count(OrganizeItemStatus.Done),
            Count(OrganizeItemStatus.Skipped),
            Count(OrganizeItemStatus.Failed, OrganizeItemStatus.RolledBack));
    }

    private async Task RelocateInTransactionAsync(TrackRelocation relocation, CancellationToken cancellationToken)
    {
        foreach (FileRelocation file in relocation.Files)
        {
            if (file.MediaFileId is { } fileId)
            {
                if (file.RelativePath is { } path)
                {
                    int slash = path.LastIndexOf('/');
                    string directory = slash < 0 ? string.Empty : path[..slash];
                    string fileName = path[(slash + 1)..];
                    int dot = fileName.LastIndexOf('.');
                    string baseName = dot <= 0 ? fileName : fileName[..dot];
                    string normalizedBaseName = baseName.ToUpperInvariant();
                    await dbContext.MediaFiles
                        .Where(item => item.Id == fileId)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(item => item.RelativePath, path)
                                .SetProperty(item => item.DirectoryPath, directory)
                                .SetProperty(item => item.FileName, fileName)
                                .SetProperty(item => item.BaseName, baseName)
                                .SetProperty(item => item.NormalizedBaseName, normalizedBaseName),
                            cancellationToken);
                    await dbContext.TrackFileSelections
                        .Where(selection => selection.MediaFileId == fileId)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(selection => selection.RelativePath, path),
                            cancellationToken);
                }
                else
                {
                    await dbContext.MediaFiles.Where(item => item.Id == fileId).ExecuteDeleteAsync(cancellationToken);
                }
            }

            await dbContext.OrganizeFileMoves
                .Where(move => move.Id == file.MoveId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(move => move.Status, OrganizeMoveStatus.Moved),
                    cancellationToken);
        }

        if (relocation.TrackId is { } trackId)
        {
            string normalizedBaseName = relocation.BaseName.ToUpperInvariant();
            await dbContext.Tracks
                .Where(track => track.Id == trackId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(track => track.DirectoryPath, relocation.DirectoryPath)
                        .SetProperty(track => track.BaseName, relocation.BaseName)
                        .SetProperty(track => track.NormalizedBaseName, normalizedBaseName),
                    cancellationToken);
        }

        List<string> createdFolders = [.. relocation.CreatedFolders];
        await dbContext.OrganizeItems
            .Where(item => item.Id == relocation.ItemId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, OrganizeItemStatus.Done)
                    .SetProperty(item => item.CreatedFolders, createdFolders)
                    .SetProperty(item => item.FinishedAt, relocation.FinishedAt),
                cancellationToken);
        await dbContext.OrganizeOperations
            .Where(operation => operation.Id == relocation.OperationId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(operation => operation.DoneItems, operation => operation.DoneItems + 1),
                cancellationToken);
    }

    private async Task<IReadOnlyList<(Guid? TrackId, string DirectoryPath, OrganizeFileRow Row)>> LoadFilesAsync(
        IQueryable<MediaFile> files,
        CancellationToken cancellationToken)
    {
        var rows = await files.AsNoTracking()
            .Select(file => new
            {
                file.Id,
                file.TrackId,
                file.DirectoryPath,
                file.RelativePath,
                file.FileName,
                file.Kind,
                file.Size,
                file.ModifiedAt,
                file.IsPrimary,
                file.IsWritable,
                file.Tags,
            })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(file => (
            file.TrackId,
            file.DirectoryPath,
            new OrganizeFileRow(
                file.Id,
                file.RelativePath,
                file.FileName,
                file.Kind,
                file.Size,
                file.ModifiedAt,
                file.IsPrimary,
                file.IsWritable,
                file.Kind is MediaFileKind.Audio or MediaFileKind.Video ? file.Tags : null)))];
    }

    private async Task<IReadOnlyList<TemplateSongRow>> LoadAsync(
        IQueryable<Track> tracks,
        CancellationToken cancellationToken)
    {
        var songs = await tracks
            .Select(track => new
            {
                track.Id,
                track.Title,
                ArtistName = track.Artist == null ? null : track.Artist.Name,
                AlbumTitle = track.Album == null ? null : track.Album.Title,
                AlbumArtistName = track.Album == null || track.Album.AlbumArtist == null
                    ? null
                    : track.Album.AlbumArtist.Name,
                IsCompilation = track.Album != null && track.Album.IsCompilation,
                Year = track.Year ?? (track.Album == null ? null : track.Album.Year),
                track.TrackNumber,
                track.DiscNumber,
                Genre = track.Genre == null ? null : track.Genre.Name,
                track.BaseName,
            })
            .ToListAsync(cancellationToken);
        Guid[] ids = [.. songs.Select(song => song.Id)];
        var files = (await dbContext.MediaFiles.AsNoTracking()
                .Where(file => file.TrackId != null && ids.Contains(file.TrackId.Value))
                .Select(file => new { TrackId = file.TrackId!.Value, file.Extension, file.Kind, file.IsPrimary })
                .ToListAsync(cancellationToken))
            .ToLookup(file => file.TrackId);

        return [.. songs.Select(song =>
        {
            var own = files[song.Id].ToList();
            string? primary = own
                .Where(file => file.IsPrimary && file.Kind is MediaFileKind.Audio or MediaFileKind.Video)
                .OrderBy(file => file.Kind)
                .Select(file => file.Extension)
                .FirstOrDefault();
            return new TemplateSongRow(
                song.Id,
                song.Title,
                song.ArtistName,
                song.AlbumTitle,
                song.AlbumArtistName,
                song.IsCompilation,
                song.Year,
                song.TrackNumber,
                song.DiscNumber,
                song.Genre,
                song.BaseName,
                primary,
                [.. own.Select(file => file.Extension).Distinct(StringComparer.Ordinal)]);
        })];
    }
}
