using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Options;
using WaveMP.Core.Organizing.OwnAlbums;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.Operations;

/// <summary>
/// Puts back what an organize run left half done when the server stopped: on start, before any job runs and while
/// holding the <see cref="LibraryMaintenanceLock"/>, every item that was running is rolled back from its journal (files
/// back to their old place, tags from the backup, created folders removed while empty); its operation becomes
/// interrupted, items done before stay done and can be undone. Operations that were planning or queued become
/// interrupted too. Then the touched folders are read again and old previews are removed.
/// </summary>
internal sealed class OrganizeRecovery(
    IOrganizeRepository repository,
    IOwnAlbumRepository ownAlbums,
    IOwnAlbumCoverStore covers,
    IMediaSourceCatalog sources,
    INoReplaceFileMover mover,
    ITagWriter tagWriter,
    ILibraryScanner scanner,
    LibraryMaintenanceLock maintenanceLock,
    IOptions<OrganizeOptions> options,
    TimeProvider timeProvider,
    ILogger<OrganizeRecovery> logger)
{
    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        using IDisposable maintenance = await maintenanceLock.AcquireAsync(cancellationToken);
        OrganizeItemRollback rollback = new(mover, tagWriter, logger);
        Dictionary<(Guid SourceId, string DirectoryPath), HashSet<string>> touched = [];
        IReadOnlyList<OrganizeItem> running = await repository.ListRunningItemsAsync(cancellationToken);
        foreach (OrganizeItem item in running)
        {
            if (sources.Find(item.SourceId) is not { } source || !Directory.Exists(source.RootPath))
            {
                logger.LogError(
                    "The interrupted organize item {ItemId} cannot be put back: its media folder is missing; the next "
                    + "start tries again",
                    item.Id);
                continue;
            }

            TagBackup? backup = item.TagBackupId is { } backupId
                ? (await repository.GetTagBackupsAsync([backupId], cancellationToken)).GetValueOrDefault(backupId)
                : null;
            string? coverSource = await CoverSourceAsync(item, cancellationToken);
            bool putBack = rollback.PutBack(source.RootPath, item, item.Moves, backup, coverSource);
            foreach (string path in item.Moves
                .SelectMany(move => new[] { move.FromPath, move.ToPath })
                .OfType<string>())
            {
                Touch(touched, item.SourceId, FolderNames.DirectoryOf(path));
            }

            if (backup is not null)
            {
                Touch(touched, item.SourceId, FolderNames.DirectoryOf(backup.RelativePath)).Add(backup.RelativePath);
            }

            if (putBack)
            {
                await repository.EndItemAsync(
                    item.Id,
                    OrganizeItemStatus.RolledBack,
                    null,
                    Now(),
                    cancellationToken);
                logger.LogWarning(
                    "The organize item {ItemId} ({Title}) was interrupted by a stop of the server and was put back",
                    item.Id,
                    item.DisplayTitle);
            }
            else
            {
                logger.LogCritical(
                    "The interrupted organize item {ItemId} ({Title}) could not be put back completely; the next start "
                    + "tries again",
                    item.Id,
                    item.DisplayTitle);
            }
        }

        IReadOnlyList<Guid> interrupted = await repository.InterruptActiveAsync(Now(), cancellationToken);
        foreach (Guid operationId in interrupted)
        {
            await RemoveLeftoversAsync(operationId, cancellationToken);
            OrganizeOperation? operation = await repository.FindOperationAsync(operationId, cancellationToken);
            if (operation?.OwnAlbumId is { } ownAlbumId)
            {
                await repository.UpdateOwnAlbumAppliedAtAsync(ownAlbumId, cancellationToken);
            }

            logger.LogWarning(
                "The organize operation {OperationId} was interrupted by a stop of the server",
                operationId);
        }

        if (touched.Count > 0)
        {
            try
            {
                await scanner.RefreshFoldersAsync(
                    [
                        .. touched.Select(folder => new FolderRefresh(
                            folder.Key.SourceId,
                            folder.Key.DirectoryPath,
                            [.. folder.Value])),
                    ],
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "The folders of the interrupted organize items could not be read again");
            }
        }

        await repository.ExpirePreviewsAsync(Now(), cancellationToken);
        await repository.DeleteOldPreviewsAsync(Now() - options.Value.PreviewRetention, cancellationToken);
    }

    /// <summary>
    /// Deletes the hidden files undo items of an interrupted operation removed but could not delete any more.
    /// </summary>
    private async Task RemoveLeftoversAsync(Guid operationId, CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizeItem> done = await repository.ListItemsByStatusAsync(
            operationId,
            OrganizeItemStatus.Done,
            cancellationToken);
        foreach (OrganizeItem item in done)
        {
            if (sources.Find(item.SourceId) is not { } source)
            {
                continue;
            }

            foreach (OrganizeFileMove move in item.Moves
                .Where(move => move.FromPath is not null && move.ToPath is null))
            {
                if (MediaPathGuard.TryResolve(
                        source.RootPath,
                        OrganizeItemRollback.RemovalTempPath(move),
                        isTarget: false,
                        out string hidden)
                    && File.Exists(hidden))
                {
                    File.Delete(hidden);
                }
            }
        }
    }

    private async Task<string?> CoverSourceAsync(OrganizeItem item, CancellationToken cancellationToken)
    {
        if (item.Kind != OrganizeItemKind.AlbumCover
            || await repository.FindOperationAsync(item.OperationId, cancellationToken)
                is not { OwnAlbumId: { } albumId }
            || await ownAlbums.FindAsync(albumId, cancellationToken) is not { CoverFileName: { } fileName })
        {
            return null;
        }

        return covers.FindPath(fileName);
    }

    private static HashSet<string> Touch(
        Dictionary<(Guid SourceId, string DirectoryPath), HashSet<string>> touched,
        Guid sourceId,
        string directoryPath)
    {
        if (!touched.TryGetValue((sourceId, directoryPath), out HashSet<string>? changed))
        {
            changed = new HashSet<string>(StringComparer.Ordinal);
            touched[(sourceId, directoryPath)] = changed;
        }

        return changed;
    }

    private DateTimeOffset Now()
    {
        return FileFingerprint.Truncate(timeProvider.GetUtcNow());
    }
}
