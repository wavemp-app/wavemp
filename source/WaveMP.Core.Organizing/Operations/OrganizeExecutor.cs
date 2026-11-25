using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Metadata.Files;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Options;
using WaveMP.Core.Organizing.OwnAlbums;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Core.Scanning.Edits;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.Operations;

internal sealed class OrganizeExecutor(
    IOrganizeRepository repository,
    IOwnAlbumRepository ownAlbums,
    IOwnAlbumCoverStore covers,
    IMediaSourceCatalog sources,
    IMediaWriteProbe writeProbe,
    IFileWriteAccess writeAccess,
    INoReplaceFileMover mover,
    ITagWriter tagWriter,
    ILibraryScanner scanner,
    LibraryWriteLock writeLock,
    LibraryMaintenanceLock maintenanceLock,
    IJobService jobs,
    OrganizeRunRegistry registry,
    IOptions<OrganizeOptions> options,
    TimeProvider timeProvider,
    ILogger<OrganizeExecutor> logger) : IOrganizeExecutor
{
    private const int ItemBatchSize = 200;

    private readonly OrganizeItemRollback _rollback = new(mover, tagWriter, logger);

    public async Task<OrganizeRunResult> RunAsync(
        Guid operationId,
        Guid? jobRunId,
        CancellationToken cancellationToken = default)
    {
        if (maintenanceLock.IsHeld)
        {
            logger.LogInformation("The organize operation {OperationId} waits for the running scan", operationId);
        }

        using IDisposable maintenance = await maintenanceLock.AcquireAsync(cancellationToken);
        OrganizeOperation? operation = await repository.FindOperationAsync(operationId, cancellationToken);
        if (operation is not { Status: OrganizeOperationStatus.Queued })
        {
            return new OrganizeRunResult(operationId, operation?.Status, 0, 0, 0);
        }

        if (!await repository.StartRunAsync(operationId, Now(), jobRunId, cancellationToken))
        {
            return new OrganizeRunResult(operationId, null, 0, 0, 0);
        }

        RunContext context = new(operation);
        OrganizeOperationStatus status = OrganizeOperationStatus.Completed;
        OrganizeErrorCode? errorCode = null;
        try
        {
            if (operation.CancelRequestedAt is not null || registry.IsCancelRequested(operationId))
            {
                status = OrganizeOperationStatus.Canceled;
            }
            else
            {
                if (operation.SkipConflicts)
                {
                    await repository.SkipConflictItemsAsync(operationId, Now(), CancellationToken.None);
                }

                context.CoverSourcePath = await FindCoverSourceAsync(operation);
                (status, errorCode) = await RunItemsAsync(context, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The organize operation {OperationId} stopped unexpectedly", operationId);
            (status, errorCode) = (OrganizeOperationStatus.Stopped, OrganizeErrorCode.Unexpected);
        }

        await FinishAsync(context, status, errorCode);
        OrganizeOperation? finished = await repository.FindOperationAsync(operationId, CancellationToken.None);
        return new OrganizeRunResult(
            operationId,
            finished?.Status ?? status,
            finished?.DoneItems ?? context.Done,
            finished?.SkippedItems ?? 0,
            finished?.FailedItems ?? 0);
    }

    private async Task<(OrganizeOperationStatus, OrganizeErrorCode?)> RunItemsAsync(
        RunContext context,
        CancellationToken cancellationToken)
    {
        Guid operationId = context.Operation.Id;
        int after = -1;
        while (true)
        {
            IReadOnlyList<OrganizeItem> items = await repository.ListItemsAfterAsync(
                operationId,
                after,
                ItemBatchSize,
                CancellationToken.None);
            if (items.Count == 0)
            {
                return (OrganizeOperationStatus.Completed, null);
            }

            foreach (OrganizeItem item in items)
            {
                after = item.Sequence;
                if (item.Status != OrganizeItemStatus.Pending)
                {
                    continue;
                }

                if (registry.IsCancelRequested(operationId))
                {
                    logger.LogInformation("The organize operation {OperationId} was canceled", operationId);
                    return (OrganizeOperationStatus.Canceled, null);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning("The organize operation {OperationId} stops: the server shuts down", operationId);
                    return (OrganizeOperationStatus.Interrupted, OrganizeErrorCode.Interrupted);
                }

                if (!await RunItemAsync(context, item))
                {
                    return (OrganizeOperationStatus.Stopped, OrganizeErrorCode.ItemFailed);
                }

                if (context.DoneSinceRefresh >= Math.Max(1, options.Value.RefreshBatchSize))
                {
                    await RefreshAsync(context);
                }
            }
        }
    }

    /// <summary>
    /// Runs one item. Returns whether it is done.
    /// </summary>
    private async Task<bool> RunItemAsync(RunContext context, OrganizeItem item)
    {
        if (sources.Find(item.SourceId) is not { IsActive: true } source)
        {
            await FailAsync(item, OrganizeConflict.SourceInactive);
            return false;
        }

        string root = source.RootPath;
        PreparedItem? prepared;
        using (await writeLock.AcquireAsync(CancellationToken.None))
        {
            (prepared, OrganizeConflict? conflict) = await PrepareAsync(context, item, root);
            if (prepared is null)
            {
                await FailAsync(item, conflict ?? OrganizeConflict.SourceMissing);
                return false;
            }

            // The journal: from here on, a crash is put back on the next start.
            await repository.BeginItemAsync(item.Id, prepared.FoldersToCreate, CancellationToken.None);
            item.CreatedFolders = [.. prepared.FoldersToCreate];
            ItemProgress progress = new();
            try
            {
                await ExecuteAsync(context, item, prepared, progress);
                await repository.RelocateTrackAsync(
                    new TrackRelocation(
                        context.Operation.Id,
                        item.Id,
                        item.Kind == OrganizeItemKind.Track ? item.TrackId : null,
                        item.ToDirectoryPath,
                        item.ToBaseName,
                        [.. item.Moves.Select(move => new FileRelocation(move.Id, move.MediaFileId, move.ToPath))],
                        prepared.FoldersToCreate,
                        Now()),
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                OrganizeConflict code = exception is ItemFailedException failed
                    ? failed.Conflict
                    : OrganizeConflict.ChangedSince;
                logger.LogError(
                    exception,
                    "The organize item {ItemId} ({Title}) failed and is put back",
                    item.Id,
                    item.DisplayTitle);
                bool putBack = _rollback.PutBack(
                    root,
                    item,
                    progress.Moves,
                    progress.Backup,
                    context.CoverSourcePath);
                if (putBack)
                {
                    await repository.EndItemAsync(
                        item.Id,
                        OrganizeItemStatus.RolledBack,
                        code,
                        Now(),
                        CancellationToken.None);
                }
                else
                {
                    // Stays running: the next start tries to put it back again.
                    logger.LogCritical(
                        "The organize item {ItemId} ({Title}) could not be put back completely; "
                            + "the next start tries again",
                        item.Id,
                        item.DisplayTitle);
                }

                context.Touch(item, prepared.AudioPath);
                return false;
            }
        }

        context.Done++;
        context.DoneSinceRefresh++;
        context.Touch(item, prepared.AudioPath);
        await CompleteItemAsync(item, root);
        return true;
    }

    /// <summary>
    /// Checks the item against disk and library again, right before it runs: every path inside the media folder and
    /// free of links, every file where and as the preview found it, every target still free, the audio file writable
    /// and its tags as in the preview. Returns what the item does, or the conflict that keeps it from running.
    /// </summary>
    private async Task<(PreparedItem? Item, OrganizeConflict? Conflict)> PrepareAsync(
        RunContext context,
        OrganizeItem item,
        string root)
    {
        if (!context.IsWritable(item.SourceId, () => writeProbe.Probe(root) == MediaFolderState.Writable))
        {
            return (null, OrganizeConflict.NotWritable);
        }

        if (item.Kind == OrganizeItemKind.Track && item.TrackId is null)
        {
            return (null, OrganizeConflict.SourceMissing);
        }

        OrganizeSongRow? song = item.TrackId is { } trackId
            ? (await repository.GetSongsAsync([trackId], CancellationToken.None)).SingleOrDefault()
            : null;
        if (item.Kind == OrganizeItemKind.Track && song is null)
        {
            return (null, OrganizeConflict.SourceMissing);
        }

        IReadOnlyDictionary<Guid, OrganizeFileRow> files = await repository.GetFilesAsync(
            [.. item.Moves.Select(move => move.MediaFileId).OfType<Guid>()],
            CancellationToken.None);
        bool isUndo = context.Operation.Kind == OrganizeOperationKind.Undo;
        OrganizeConflict changed = isUndo ? OrganizeConflict.ChangedSince : OrganizeConflict.SourceMissing;

        foreach (OrganizeFileMove move in item.Moves)
        {
            if ((move.FromPath is { } from && !MediaPathGuard.TryResolve(root, from, isTarget: false, out _))
                || (move.ToPath is { } to && !MediaPathGuard.TryResolve(root, to, isTarget: true, out _))
                || (move.TempPath is { } temporary
                    && !MediaPathGuard.TryResolve(root, temporary, isTarget: false, out _))
                || (move.FromPath is not null && move.ToPath is null
                    && !MediaPathGuard.TryResolve(
                        root,
                        OrganizeItemRollback.RemovalTempPath(move),
                        isTarget: false,
                        out _)))
            {
                return (null, OrganizeConflict.UnsafePath);
            }

            FileFingerprint? found = move.FromPath is { } source
                ? FingerprintOf(root, source)
                : context.CoverSourcePath is { } cover ? FingerprintOf(cover) : null;
            if (found is null || !found.Matches(move.Size, move.ModifiedAt))
            {
                return (null, move.FromPath is null ? OrganizeConflict.ChangedSince : changed);
            }

            if (move.MediaFileId is { } fileId
                && (!files.TryGetValue(fileId, out OrganizeFileRow? file)
                    || !string.Equals(file.RelativePath, move.FromPath, StringComparison.Ordinal)))
            {
                return (null, changed);
            }

            string? hidden = move.ToPath is null && move.FromPath is not null
                ? OrganizeItemRollback.RemovalTempPath(move)
                : move.TempPath;
            if (hidden is not null && Exists(root, hidden))
            {
                return (null, OrganizeConflict.TargetExists);
            }
        }

        if (TargetConflict(root, item, song) is { } targetConflict)
        {
            return (null, targetConflict);
        }

        (IReadOnlyList<string>? folders, bool blocked) = FoldersToCreate(root, item);
        if (blocked || folders is null)
        {
            return (null, OrganizeConflict.TargetExists);
        }

        OrganizeFileRow? audio = null;
        string? audioPath = null;
        if (item.TagChanges is { IsEmpty: false })
        {
            audio = song?.Files.FirstOrDefault(file => file.Kind == MediaFileKind.Audio && file.IsPrimary)
                ?? song?.Files.FirstOrDefault(file => file.Kind == MediaFileKind.Audio);
            if (audio is null)
            {
                return (null, OrganizeConflict.NoAudio);
            }

            if (!MediaPathGuard.TryResolve(root, audio.RelativePath, isTarget: false, out string audioFull)
                || FingerprintOf(audioFull) is not { } current
                || !current.Matches(audio.Size, audio.ModifiedAt)
                || (item.TagsBefore is { } before && !OrganizeTags.StillMatches(before, audio.Tags)))
            {
                return (null, OrganizeConflict.ChangedSince);
            }

            if (!writeAccess.IsWritable(audioFull))
            {
                return (null, OrganizeConflict.NotWritable);
            }

            audioPath = item.Moves.FirstOrDefault(move => move.MediaFileId == audio.MediaFileId)?.ToPath
                ?? audio.RelativePath;
        }

        return (new PreparedItem(folders, audio, audioPath), null);
    }

    /// <summary>
    /// Whether a target is taken: a file or folder with the name of a moved file, or (for a song) a file with its new
    /// base name that is not one of its own files, compared without regard to case and normalization.
    /// </summary>
    private static OrganizeConflict? TargetConflict(string root, OrganizeItem item, OrganizeSongRow? song)
    {
        HashSet<string> ownPaths =
        [
            .. item.Moves.Select(move => move.FromPath).OfType<string>().Select(FolderNames.Key),
        ];
        if (song is not null)
        {
            ownPaths.UnionWith(song.Files.Select(file => FolderNames.Key(file.RelativePath)));
        }

        foreach (IGrouping<string, OrganizeFileMove> folder in item.Moves
            .Where(move => move.ToPath is not null)
            .GroupBy(move => FolderNames.DirectoryOf(move.ToPath!), StringComparer.Ordinal))
        {
            if (!MediaPathGuard.TryResolve(root, folder.Key, isTarget: false, out string fullPath)
                && folder.Key.Length > 0)
            {
                return OrganizeConflict.UnsafePath;
            }

            string directory = folder.Key.Length == 0 ? root : fullPath;
            if (!Directory.Exists(directory))
            {
                continue;
            }

            HashSet<string> names = [.. folder.Select(move => FolderNames.Key(FolderNames.FileNameOf(move.ToPath!)))];
            string? baseKey = item.Kind == OrganizeItemKind.Track ? FolderNames.Key(item.ToBaseName) : null;
            foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                if (ownPaths.Contains(FolderNames.Key(FolderNames.Combine(folder.Key, entry.Name))))
                {
                    continue;
                }

                string key = FolderNames.Key(entry.Name);
                if (names.Contains(key)
                    || (baseKey is not null && entry is FileInfo
                        && FolderNames.Key(FolderNames.BaseNameOf(entry.Name)) == baseKey))
                {
                    return item.Kind == OrganizeItemKind.AlbumCover
                        ? OrganizeConflict.CoverExists
                        : OrganizeConflict.TargetExists;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The folders the item has to create, outermost first; blocked if a file stands where a folder is needed or a
    /// folder of another spelling (case, normalization) appeared since the preview.
    /// </summary>
    private static (IReadOnlyList<string>? Folders, bool Blocked) FoldersToCreate(string root, OrganizeItem item)
    {
        List<string> create = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string target in item.Moves.Select(move => move.ToPath).OfType<string>())
        {
            string current = string.Empty;
            foreach (string segment in FolderNames.DirectoryOf(target)
                .Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                string parent = current;
                current = FolderNames.Combine(current, segment);
                if (!seen.Add(current))
                {
                    continue;
                }

                if (!MediaPathGuard.TryResolve(root, current, isTarget: true, out string fullPath))
                {
                    return (null, true);
                }

                if (Directory.Exists(fullPath) && HasExactName(root, parent, segment))
                {
                    continue;
                }

                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    // A file, or a folder whose name differs in case or normalization.
                    return (null, true);
                }

                create.Add(current);
            }
        }

        return (create, false);
    }

    private static bool HasExactName(string root, string parent, string name)
    {
        string directory = parent.Length == 0
            ? root
            : MediaPathGuard.TryResolve(root, parent, isTarget: false, out string fullPath) ? fullPath : root;
        return new DirectoryInfo(directory).EnumerateDirectories()
            .Any(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Changes the disk: the tags (after their backup), the new folders and the files. Records every file it moved in
    /// <paramref name="progress"/>, so a failure puts back exactly these.
    /// </summary>
    private async Task ExecuteAsync(RunContext context, OrganizeItem item, PreparedItem prepared, ItemProgress progress)
    {
        string root = sources.Find(item.SourceId)!.RootPath;
        if (prepared.Audio is { } audio && item.TagChanges is { IsEmpty: false } changes)
        {
            MediaPathGuard.TryResolve(root, audio.RelativePath, isTarget: false, out string audioFull);
            TagSnapshotResult snapshot = tagWriter.ReadSnapshot(audioFull);
            if (!snapshot.IsRead)
            {
                throw new ItemFailedException(
                    OrganizeConflict.NotWritable,
                    $"The tags cannot be read: {snapshot.Status}");
            }

            TagBackup backup = TagBackups.Create(snapshot.Snapshot, audio.MediaFileId, audio.RelativePath, Now());
            await repository.AddItemTagBackupAsync(
                item.Id,
                backup,
                context.Operation.Kind == OrganizeOperationKind.OwnAlbum ? context.Operation.OwnAlbumId : null,
                item.TrackId,
                CancellationToken.None);
            progress.Backup = backup;

            TagWriteStatus status = tagWriter.Write(audioFull, MediaFileKind.Audio, OrganizeTags.ToTagChanges(changes));
            if (status != TagWriteStatus.Written)
            {
                throw new ItemFailedException(OrganizeConflict.NotWritable, $"The tags cannot be written: {status}");
            }

            if (item.Moves.FirstOrDefault(move => move.MediaFileId == audio.MediaFileId) is { } audioMove
                && FingerprintOf(audioFull) is { } written)
            {
                // The moves check the file they move against the journal; the tags changed it.
                await repository.UpdateMoveFingerprintAsync(
                    audioMove.Id,
                    written.Size,
                    written.ModifiedAt,
                    CancellationToken.None);
                audioMove.Size = written.Size;
                audioMove.ModifiedAt = written.ModifiedAt;
            }
        }

        foreach (string folder in prepared.FoldersToCreate)
        {
            MediaPathGuard.TryResolve(root, folder, isTarget: true, out string fullPath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }

        foreach (OrganizeFileMove move in item.Moves.OrderBy(move => move.Sequence))
        {
            Move(root, move, context.CoverSourcePath, progress);
        }
    }

    private void Move(string root, OrganizeFileMove move, string? coverSourcePath, ItemProgress progress)
    {
        if (move.FromPath is null)
        {
            // The cover of an own album, copied from the data folder into a file created exclusively.
            MediaPathGuard.TryResolve(root, move.ToPath!, isTarget: true, out string coverTarget);
            FileMoveResult copied = mover.Copy(coverSourcePath!, coverTarget);
            Check(copied, OrganizeConflict.CoverExists, move);
            progress.Moves.Add(move);
            return;
        }

        MediaPathGuard.TryResolve(root, move.FromPath, isTarget: false, out string fromFull);
        if (FingerprintOf(fromFull) is not { } found || !found.Matches(move.Size, move.ModifiedAt))
        {
            throw new ItemFailedException(OrganizeConflict.ChangedSince, $"{move.FromPath} changed");
        }

        if (move.ToPath is null)
        {
            // Removed by an undo: first to a hidden name, deleted once the item is recorded.
            MediaPathGuard.TryResolve(
                root,
                OrganizeItemRollback.RemovalTempPath(move),
                isTarget: false,
                out string hidden);
            Check(mover.Move(fromFull, hidden), OrganizeConflict.TargetExists, move);
            progress.Moves.Add(move);
            return;
        }

        // A move is put back on failure only once it happened (at least its first step), so a file someone else put at
        // the target is never touched.
        MediaPathGuard.TryResolve(root, move.ToPath, isTarget: true, out string toFull);
        if (move.TempPath is { } temporary)
        {
            // Only the case or normalization of the name changes: through a hidden name, so every file system renames.
            MediaPathGuard.TryResolve(root, temporary, isTarget: false, out string temporaryFull);
            Check(mover.Move(fromFull, temporaryFull), OrganizeConflict.TargetExists, move);
            progress.Moves.Add(move);
            Check(mover.Move(temporaryFull, toFull), OrganizeConflict.TargetExists, move);
            return;
        }

        Check(mover.Move(fromFull, toFull), OrganizeConflict.TargetExists, move);
        progress.Moves.Add(move);
    }

    private static void Check(FileMoveResult result, OrganizeConflict taken, OrganizeFileMove move)
    {
        if (result.IsMoved)
        {
            return;
        }

        OrganizeConflict conflict = result.Status switch
        {
            FileMoveStatus.TargetExists => taken,
            FileMoveStatus.SourceMissing => OrganizeConflict.SourceMissing,
            _ => OrganizeConflict.NotWritable,
        };
        throw new ItemFailedException(
            conflict,
            $"{move.FromPath} -> {move.ToPath}: {result.Status} ({result.ErrorNumber})");
    }

    /// <summary>
    /// After an item is recorded: deletes the files an undo removed and the folders the moves left empty.
    /// </summary>
    private async Task CompleteItemAsync(OrganizeItem item, string root)
    {
        foreach (OrganizeFileMove move in item.Moves.Where(move => move.FromPath is not null && move.ToPath is null))
        {
            if (MediaPathGuard.TryResolve(
                    root,
                    OrganizeItemRollback.RemovalTempPath(move),
                    isTarget: false,
                    out string hidden))
            {
                TryDelete(hidden);
            }
        }

        List<string> removed = [];
        foreach (string folder in item.Moves
            .Select(move => move.FromPath)
            .OfType<string>()
            .Select(FolderNames.DirectoryOf)
            .Where(folder => folder.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(folder => folder.Length))
        {
            removed.AddRange(EmptyFolderCleaner.RemoveEmptyFolders(root, folder));
        }

        if (removed.Count > 0)
        {
            await repository.SetRemovedFoldersAsync(
                item.Id,
                [.. removed.Distinct(StringComparer.Ordinal)],
                CancellationToken.None);
        }
    }

    private async Task FailAsync(OrganizeItem item, OrganizeConflict conflict)
    {
        logger.LogWarning(
            "The organize item {ItemId} ({Title}) cannot run: {Conflict}; nothing of it was changed",
            item.Id,
            item.DisplayTitle,
            conflict);
        await repository.EndItemAsync(item.Id, OrganizeItemStatus.Failed, conflict, Now(), CancellationToken.None);
    }

    private async Task FinishAsync(RunContext context, OrganizeOperationStatus status, OrganizeErrorCode? errorCode)
    {
        OrganizeOperation operation = context.Operation;
        try
        {
            await RefreshAsync(context);
        }
        finally
        {
            await repository.FinishRunAsync(operation.Id, status, errorCode, Now(), CancellationToken.None);
            if (operation is { Kind: OrganizeOperationKind.Undo, UndoOfId: { } original }
                && status == OrganizeOperationStatus.Completed)
            {
                await repository.SetUndoneByAsync(original, operation.Id, CancellationToken.None);
            }

            if (operation.OwnAlbumId is { } ownAlbumId)
            {
                await repository.UpdateOwnAlbumAppliedAtAsync(ownAlbumId, CancellationToken.None);
            }

            registry.Clear(operation.Id);
            await repository.DeleteOldPreviewsAsync(Now() - options.Value.PreviewRetention, CancellationToken.None);
            logger.LogInformation(
                "Organize operation {OperationId} ({Kind}) ended {Status}: {Done} items done",
                operation.Id,
                operation.Kind,
                status,
                context.Done);
        }

        if (context.Done > 0)
        {
            await StartCoverCacheAsync();
        }
    }

    /// <summary>
    /// Brings the library in line with the folders the items touched since the last time.
    /// </summary>
    private async Task RefreshAsync(RunContext context)
    {
        IReadOnlyList<FolderRefresh> folders = context.TakeFolders();
        if (folders.Count == 0)
        {
            return;
        }

        try
        {
            await scanner.RefreshFoldersAsync(folders, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // The files are moved and recorded; the next scan reads what is missing.
            logger.LogError(exception, "The library could not read the organized folders again");
        }
    }

    private async Task StartCoverCacheAsync()
    {
        try
        {
            await jobs.StartRunAsync(
                new JobRunRequest(JobIds.CoverCache, JobRunTrigger.LibraryScan),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot start the job {JobId} after organizing the files", JobIds.CoverCache);
        }
    }

    private async Task<string?> FindCoverSourceAsync(OrganizeOperation operation)
    {
        if (operation.OwnAlbumId is not { } albumId
            || await ownAlbums.FindAsync(albumId, CancellationToken.None) is not { CoverFileName: { } fileName })
        {
            return null;
        }

        return covers.FindPath(fileName);
    }

    private static FileFingerprint? FingerprintOf(string root, string relativePath)
    {
        return MediaPathGuard.TryResolve(root, relativePath, isTarget: false, out string fullPath)
            ? FingerprintOf(fullPath)
            : null;
    }

    private static FileFingerprint? FingerprintOf(string fullPath)
    {
        FileInfo info = new(fullPath);
        return info.Exists ? new FileFingerprint(info.Length, FileFingerprint.Truncate(info.LastWriteTimeUtc)) : null;
    }

    private static bool Exists(string root, string relativePath)
    {
        return MediaPathGuard.TryResolve(root, relativePath, isTarget: false, out string fullPath)
            && (File.Exists(fullPath) || Directory.Exists(fullPath));
    }

    private void TryDelete(string fullPath)
    {
        try
        {
            File.Delete(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The removed file {File} cannot be deleted", fullPath);
        }
    }

    private DateTimeOffset Now()
    {
        return FileFingerprint.Truncate(timeProvider.GetUtcNow());
    }

    /// <summary>
    /// What a checked item does.
    /// </summary>
    /// <param name="FoldersToCreate">Folders to create, outermost first.</param>
    /// <param name="Audio">The audio file whose tags are written.</param>
    /// <param name="AudioPath">Its path after the item.</param>
    private sealed record PreparedItem(
        IReadOnlyList<string> FoldersToCreate,
        OrganizeFileRow? Audio,
        string? AudioPath);

    /// <summary>
    /// What an item changed so far.
    /// </summary>
    private sealed class ItemProgress
    {
        public List<OrganizeFileMove> Moves { get; } = [];

        public TagBackup? Backup { get; set; }
    }

    /// <summary>
    /// The state of one run.
    /// </summary>
    private sealed class RunContext(OrganizeOperation operation)
    {
        private readonly Dictionary<(Guid SourceId, string DirectoryPath), HashSet<string>> _folders = [];
        private readonly Dictionary<Guid, bool> _writable = [];

        public OrganizeOperation Operation { get; } = operation;

        public string? CoverSourcePath { get; set; }

        public int Done { get; set; }

        public int DoneSinceRefresh { get; set; }

        /// <summary>
        /// Whether the folder of the source can be written, probed once per run (the probe creates a file).
        /// </summary>
        public bool IsWritable(Guid sourceId, Func<bool> probe)
        {
            if (!_writable.TryGetValue(sourceId, out bool writable))
            {
                writable = probe();
                _writable[sourceId] = writable;
            }

            return writable;
        }

        /// <summary>
        /// Remembers the old and new folders of an item and the audio file whose tags were written.
        /// </summary>
        public void Touch(OrganizeItem item, string? audioPath)
        {
            foreach (string path in item.Moves
                .SelectMany(move => new[] { move.FromPath, move.ToPath })
                .OfType<string>())
            {
                Folder(item.SourceId, FolderNames.DirectoryOf(path));
            }

            Folder(item.SourceId, item.FromDirectoryPath);
            Folder(item.SourceId, item.ToDirectoryPath);
            if (audioPath is not null)
            {
                Folder(item.SourceId, FolderNames.DirectoryOf(audioPath)).Add(audioPath);
            }
        }

        public IReadOnlyList<FolderRefresh> TakeFolders()
        {
            List<FolderRefresh> folders = [.. _folders.Select(folder =>
                new FolderRefresh(folder.Key.SourceId, folder.Key.DirectoryPath, [.. folder.Value]))];
            _folders.Clear();
            DoneSinceRefresh = 0;
            return folders;
        }

        private HashSet<string> Folder(Guid sourceId, string directoryPath)
        {
            if (!_folders.TryGetValue((sourceId, directoryPath), out HashSet<string>? changed))
            {
                changed = new HashSet<string>(StringComparer.Ordinal);
                _folders[(sourceId, directoryPath)] = changed;
            }

            return changed;
        }
    }

    /// <summary>
    /// An item failed for a reason the log shows with its conflict.
    /// </summary>
    private sealed class ItemFailedException(OrganizeConflict conflict, string message) : Exception(message)
    {
        public OrganizeConflict Conflict { get; } = conflict;
    }
}
