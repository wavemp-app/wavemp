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
                track.Genre,
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

        return await tracks.OrderBy(track => track.Id).Select(track => track.Id).Take(count).ToListAsync(cancellationToken);
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
                track.Genre,
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
