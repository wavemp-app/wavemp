using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Metadata.Files;
using WaveMP.Core.Metadata.Resolutions;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Jobs;
using WaveMP.Core.Organizing.Operations;
using WaveMP.Core.Organizing.Options;
using WaveMP.Core.Organizing.OwnAlbums;
using WaveMP.Core.Organizing.Templates;
using WaveMP.Core.Scanning.Options;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Services;
using WaveMP.Core.Settings.Templates;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.Previews;

internal sealed class OrganizePreviewService(
    IOrganizeRepository repository,
    IJobService jobs,
    IJobRunTracker tracker,
    OrganizeRunRegistry registry,
    IOwnAlbumRepository ownAlbums,
    IOwnAlbumCoverStore covers,
    IOrganizingSettingsService organizingSettings,
    ILibrarySettingsService librarySettings,
    IMediaSourceCatalog sources,
    IMediaWriteProbe writeProbe,
    IFileWriteAccess writeAccess,
    IOptions<OrganizeOptions> options,
    IOptions<ScanOptions> scanOptions,
    TimeProvider timeProvider,
    ILogger<OrganizePreviewService> logger) : IOrganizePreviewService
{
    /// <summary>
    /// The fields an own album writes into every song.
    /// </summary>
    private static readonly OrganizeTagField[] _albumFields =
    [
        OrganizeTagField.Album,
        OrganizeTagField.AlbumArtist,
        OrganizeTagField.TrackNumber,
        OrganizeTagField.TrackCount,
        OrganizeTagField.DiscNumber,
    ];

    private static readonly OrganizeTagField[] _allFields = Enum.GetValues<OrganizeTagField>();

    public async Task<OrganizePreviewResult> PreviewOwnAlbumAsync(
        Guid ownAlbumId,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        OwnAlbumRow? album = await ownAlbums.FindAsync(ownAlbumId, cancellationToken);
        if (album is null)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.OwnAlbumNotFound);
        }

        IReadOnlyList<OwnAlbumMemberRow> members = await ownAlbums.ListMembersAsync(ownAlbumId, cancellationToken);
        if (!members.Any(member => member.State == OwnAlbumTrackState.Member))
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.AlbumEmpty);
        }

        if (await repository.FindActiveOperationIdAsync(cancellationToken) is not null)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.Busy);
        }

        OrganizingSettings settings = await organizingSettings.GetAsync(cancellationToken);
        if (LayoutTemplateSyntax.Parse(settings.LayoutTemplate).Template is not { } template)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.TemplateInvalid);
        }

        IReadOnlyList<OrganizeSongRow> songs = await repository.GetSongsAsync(
            [.. members.Select(member => member.TrackId)],
            cancellationToken);
        if (UsableSources(songs.Select(song => song.SourceId)) is not { } usable)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.MediaReadOnly);
        }

        var byId = songs.ToDictionary(song => song.TrackId);
        OwnAlbumMemberRow[] current = [.. members
            .Where(member => member.State == OwnAlbumTrackState.Member && byId.ContainsKey(member.TrackId))
            .OrderBy(member => member.Position)];
        // A song taken out and put into another own album since is placed by that album, not restored here.
        IReadOnlyList<OwnAlbumCandidateRow> taken = await ownAlbums.ListCandidatesAsync(
            [.. members.Where(member => member.State == OwnAlbumTrackState.Removed).Select(member => member.TrackId)],
            cancellationToken);
        HashSet<Guid> elsewhere = [.. taken
            .Where(candidate => candidate.MemberOfOwnAlbumId is { } other && other != ownAlbumId)
            .Select(candidate => candidate.TrackId)];
        OwnAlbumMemberRow[] removed = [.. members
            .Where(member => member.State == OwnAlbumTrackState.Removed && byId.ContainsKey(member.TrackId)
                && !elsewhere.Contains(member.TrackId))];
        IReadOnlyDictionary<Guid, TagBackup> backups = await repository.GetTagBackupsAsync(
            [.. removed.Select(member => member.OriginalTagBackupId).OfType<Guid>()],
            cancellationToken);
        bool merges = await repository.CountOtherAlbumTracksAsync(
            AlbumKeys.Create(album.Title, album.AlbumArtist),
            [.. current.Select(member => member.TrackId)],
            cancellationToken) > 0;

        var operationId = Guid.CreateVersion7();
        OrganizePlanner planner = new(
            operationId,
            template,
            scanOptions.Value.MaxPathBytes,
            new OrganizeDisk(sources, writeAccess),
            usable,
            TitleCleaningModes.Of(settings));
        List<OrganizeItem> items = [];
        items.AddRange(planner.PlanSongs(
            current.Select((member, index) => MemberSong(album, byId[member.TrackId], index, current.Length, merges)),
            skipUnchanged: true));
        items.AddRange(planner.PlanSongs(
            removed.Select(member => RemovedSong(
                byId[member.TrackId],
                member.OriginalTagBackupId is { } backupId ? backups.GetValueOrDefault(backupId) : null)),
            skipUnchanged: true));

        if (CoverOf(album) is { } cover)
        {
            if (template.IsFolderBased)
            {
                LibrarySettings library = await librarySettings.GetAsync(cancellationToken);
                items.AddRange(planner.PlanCover(
                    cover,
                    [.. current.Select(member => member.TrackId)],
                    library.AlbumCoverNames));
            }
            else if (items.FirstOrDefault(item => item.Kind == OrganizeItemKind.Track) is { } first)
            {
                first.Warnings.Add(OrganizeWarning.CoverNotWritten);
            }
        }
        else
        {
            items.AddRange(await PlanFolderFilesAsync(planner, cancellationToken));
        }

        OrganizeOperation operation = NewOperation(
            OrganizeOperationKind.OwnAlbum,
            operationId,
            settings.LayoutTemplate,
            userId);
        operation.OwnAlbumId = album.Id;
        operation.DisplayTitle = album.Title;
        return await SaveAsync(operation, items, cancellationToken);
    }

    public async Task<OrganizePreviewResult> PreviewLibraryAsync(
        Guid? sourceId,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (sourceId is { } id && sources.Find(id) is null)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.SourceNotFound);
        }

        if (await repository.FindActiveOperationIdAsync(cancellationToken) is not null)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.Busy);
        }

        OrganizingSettings settings = await organizingSettings.GetAsync(cancellationToken);
        if (!LayoutTemplateSyntax.Parse(settings.LayoutTemplate).IsValid)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.TemplateInvalid);
        }

        IEnumerable<Guid> involved = sources.Sources
            .Where(source => source.IsActive && (sourceId is null || source.Id == sourceId))
            .Select(source => source.Id);
        if (UsableSources(involved) is null)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.MediaReadOnly);
        }

        if (tracker.GetState(JobIds.LibraryOrganize) != JobActivityState.Idle)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.Busy);
        }

        OrganizeOperation operation = NewOperation(
            OrganizeOperationKind.Library,
            Guid.CreateVersion7(),
            settings.LayoutTemplate,
            userId);
        operation.Status = OrganizeOperationStatus.Planning;
        operation.SourceId = sourceId;
        await repository.AddOperationAsync(operation, cancellationToken);

        // Planned by the organize job, with its progress in the counters of the operation.
        JobRunStartResult started = await jobs.StartRunAsync(
            new JobRunRequest(
                JobIds.LibraryOrganize,
                JobRunTrigger.Manual,
                LibraryOrganizeJob.CreatePayload(operation.Id)),
            CancellationToken.None);
        if (!started.IsSuccess)
        {
            await repository.DeletePreviewAsync(operation.Id, CancellationToken.None);
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.Busy);
        }

        return new OrganizePreviewResult(
            OrganizePreviewStatus.Created,
            await repository.FindOperationAsync(operation.Id, cancellationToken));
    }

    public async Task PlanLibraryAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        OrganizeOperation? operation = await repository.FindOperationAsync(operationId, cancellationToken);
        if (operation is not { Status: OrganizeOperationStatus.Planning })
        {
            return;
        }

        try
        {
            OrganizingSettings settings = await organizingSettings.GetAsync(cancellationToken);
            if (LayoutTemplateSyntax.Parse(operation.Template).Template is not { } template)
            {
                await repository.FinishPlanningAsync(
                    operationId,
                    OrganizeOperationStatus.Failed,
                    OrganizeErrorCode.Unexpected,
                    Now(),
                    null,
                    cancellationToken);
                return;
            }

            IEnumerable<Guid> involved = sources.Sources
                .Where(source => source.IsActive && (operation.SourceId is null || source.Id == operation.SourceId))
                .Select(source => source.Id);
            IReadOnlySet<Guid> usable = UsableSources(involved) ?? new HashSet<Guid>();
            OrganizePlanner planner = new(
                operationId,
                template,
                scanOptions.Value.MaxPathBytes,
                new OrganizeDisk(sources, writeAccess),
                usable,
                TitleCleaningModes.Of(settings));
            Guid? after = null;
            int batchSize = Math.Max(1, options.Value.PlanningBatchSize);
            while (true)
            {
                if (operation.CancelRequestedAt is not null || registry.IsCancelRequested(operationId))
                {
                    await repository.FinishPlanningAsync(
                        operationId,
                        OrganizeOperationStatus.Canceled,
                        null,
                        Now(),
                        null,
                        CancellationToken.None);
                    registry.Clear(operationId);
                    return;
                }

                IReadOnlyList<Guid> ids = await repository.ListTrackIdsAsync(
                    operation.SourceId,
                    after,
                    batchSize,
                    cancellationToken);
                if (ids.Count == 0)
                {
                    break;
                }

                after = ids[^1];
                IReadOnlyList<OrganizeSongRow> songs = await repository.GetSongsAsync(ids, cancellationToken);
                IReadOnlyList<OrganizeItem> items = planner.PlanSongs(
                    songs.OrderBy(song => song.TrackId).Select(song => new PlannedSong(song, ValuesOf(song))),
                    skipUnchanged: true);
                await repository.AddItemsAsync(items, cancellationToken);
            }

            await repository.AddItemsAsync(await PlanFolderFilesAsync(planner, cancellationToken), cancellationToken);
            DateTimeOffset now = Now();
            await repository.FinishPlanningAsync(
                operationId,
                OrganizeOperationStatus.Previewed,
                null,
                now,
                now + options.Value.PreviewLifetime,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Cannot plan the organize operation {OperationId}", operationId);
            await repository.FinishPlanningAsync(
                operationId,
                OrganizeOperationStatus.Failed,
                OrganizeErrorCode.Unexpected,
                Now(),
                null,
                CancellationToken.None);
        }
    }

    public async Task<OrganizePreviewResult> PreviewUndoAsync(
        Guid operationId,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        OrganizeOperation? original = await repository.FindOperationAsync(operationId, cancellationToken);
        if (original is null)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.OperationNotFound);
        }

        if (original.UndoneById is not null)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.AlreadyUndone);
        }

        if (original.Kind == OrganizeOperationKind.Undo
            || original.Status is not (OrganizeOperationStatus.Completed
                or OrganizeOperationStatus.Stopped
                or OrganizeOperationStatus.Canceled
                or OrganizeOperationStatus.Interrupted))
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.NotUndoable);
        }

        IReadOnlyList<OrganizeItem> done = await repository.ListItemsByStatusAsync(
            operationId,
            OrganizeItemStatus.Done,
            cancellationToken);
        if (done.Count == 0)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.NotUndoable);
        }

        if (await repository.FindActiveOperationIdAsync(cancellationToken) is not null)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.Busy);
        }

        if (UsableSources(done.Select(item => item.SourceId)) is not { } usable)
        {
            return OrganizePreviewResult.Failed(OrganizePreviewStatus.MediaReadOnly);
        }

        IReadOnlyDictionary<Guid, TagBackup> backups = await repository.GetTagBackupsAsync(
            [.. done.Select(item => item.TagBackupId).OfType<Guid>()],
            cancellationToken);
        IReadOnlyDictionary<Guid, OrganizeFileRow> files = await repository.GetFilesAsync(
            [.. done.SelectMany(item => item.Moves).Select(move => move.MediaFileId).OfType<Guid>()
                .Concat(backups.Values.Select(backup => backup.MediaFileId).OfType<Guid>())],
            cancellationToken);

        var undoId = Guid.CreateVersion7();
        LayoutTemplate template = LayoutTemplateSyntax.Parse(original.Template).Template
            ?? LayoutTemplateSyntax.Parse(OrganizingSettings.DefaultLayoutTemplate).Template!;
        OrganizePlanner planner = new(
            undoId,
            template,
            scanOptions.Value.MaxPathBytes,
            new OrganizeDisk(sources, writeAccess),
            usable);
        IReadOnlyList<OrganizeItem> items = planner.PlanUndo(done, files, backups);

        OrganizeOperation operation = NewOperation(OrganizeOperationKind.Undo, undoId, original.Template, userId);
        operation.UndoOfId = original.Id;
        operation.OwnAlbumId = original.OwnAlbumId;
        operation.SourceId = original.SourceId;
        operation.DisplayTitle = original.DisplayTitle;
        return await SaveAsync(operation, items, cancellationToken);
    }

    private static PlannedSong MemberSong(OwnAlbumRow album, OrganizeSongRow song, int index, int count, bool merges)
    {
        int? year = album.Year ?? song.Year;
        string? genre = album.Genre ?? song.Genre;
        TemplateValues values = new(
            song.Title,
            song.ArtistName,
            album.AlbumArtist,
            album.Title,
            year,
            index + 1,
            null,
            genre);
        List<OrganizeTagField> fields = [.. _albumFields];
        if (album.Year is not null)
        {
            fields.Add(OrganizeTagField.Year);
        }

        if (album.Genre is not null)
        {
            fields.Add(OrganizeTagField.Genre);
        }

        OrganizeTagValues desired = new()
        {
            Fields = fields,
            Album = album.Title,
            AlbumArtists = [album.AlbumArtist],
            TrackNumber = index + 1,
            TrackCount = count,
            DiscNumber = null,
            Year = album.Year,
            Genres = album.Genre is null ? [] : [album.Genre],
        };
        return new PlannedSong(
            song,
            values,
            OrganizeTags.Difference(desired, AudioOf(song)?.Tags),
            RequiresAudio: true,
            merges ? [OrganizeWarning.AlbumMerges] : []);
    }

    /// <summary>
    /// A song taken out of an applied own album: it gets the tags of its backup back and is placed by them.
    /// </summary>
    private static PlannedSong RemovedSong(OrganizeSongRow song, TagBackup? backup)
    {
        if (backup is null)
        {
            return new PlannedSong(song, ValuesOf(song));
        }

        TemplateValues values = new(
            song.Title,
            song.ArtistName,
            backup.AlbumArtists.FirstOrDefault(),
            backup.Album,
            backup.Year ?? song.Year,
            backup.TrackNumber,
            backup.DiscNumber,
            backup.Genres.FirstOrDefault() ?? song.Genre);
        OrganizeTagValues changes = OrganizeTags.Difference(
            OrganizeTags.FromBackup(_allFields, backup),
            AudioOf(song)?.Tags);
        return new PlannedSong(song, values, changes, RequiresAudio: true);
    }

    internal static TemplateValues ValuesOf(OrganizeSongRow song)
    {
        return new TemplateValues(
            song.Title,
            song.ArtistName,
            song.AlbumArtistName,
            song.AlbumTitle,
            song.Year,
            song.TrackNumber,
            song.DiscNumber,
            song.Genre,
            song.IsCompilation);
    }

    private static OrganizeFileRow? AudioOf(OrganizeSongRow song)
    {
        return song.Files.FirstOrDefault(file => file.Kind == MediaFileKind.Audio && file.IsPrimary)
            ?? song.Files.FirstOrDefault(file => file.Kind == MediaFileKind.Audio);
    }

    private PlannedCover? CoverOf(OwnAlbumRow album)
    {
        if (album.CoverFileName is not { } fileName || covers.FindPath(fileName) is not { } path)
        {
            return null;
        }

        FileInfo info = new(path);
        return new PlannedCover(
            path,
            FolderNames.ExtensionOf(fileName) ?? "jpg",
            info.Length,
            FileFingerprint.Truncate(info.LastWriteTimeUtc),
            album.Title);
    }

    private async Task<IReadOnlyList<OrganizeItem>> PlanFolderFilesAsync(
        OrganizePlanner planner,
        CancellationToken cancellationToken)
    {
        List<OrganizeFolderRow> folders = [];
        foreach (IGrouping<Guid, (Guid SourceId, string DirectoryPath)> source in planner.SourceFolders
            .GroupBy(folder => folder.SourceId))
        {
            folders.AddRange(await repository.GetFoldersAsync(
                source.Key,
                [.. source.Select(folder => folder.DirectoryPath)],
                cancellationToken));
        }

        return planner.PlanFolderFiles(folders);
    }

    /// <summary>
    /// The sources of <paramref name="sourceIds"/> that can be organized (active, folder present and writable);
    /// <c>null</c> if one of them is active but cannot be written.
    /// </summary>
    private IReadOnlySet<Guid>? UsableSources(IEnumerable<Guid> sourceIds)
    {
        HashSet<Guid> usable = [];
        foreach (Guid sourceId in sourceIds.Distinct())
        {
            if (sources.Find(sourceId) is not { IsActive: true } source)
            {
                continue;
            }

            switch (writeProbe.Probe(source.RootPath))
            {
                case MediaFolderState.ReadOnly:
                    return null;
                case MediaFolderState.Writable:
                    usable.Add(sourceId);
                    break;
            }
        }

        return usable;
    }

    private OrganizeOperation NewOperation(
        OrganizeOperationKind kind,
        Guid id,
        string template,
        Guid? userId)
    {
        return new OrganizeOperation
        {
            Id = id,
            Kind = kind,
            Status = OrganizeOperationStatus.Previewed,
            Template = template,
            CreatedByUserId = userId,
            CreatedAt = Now(),
        };
    }

    private async Task<OrganizePreviewResult> SaveAsync(
        OrganizeOperation operation,
        IReadOnlyList<OrganizeItem> items,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = Now();
        operation.Items = [.. items];
        operation.TotalItems = items.Count;
        operation.ConflictItems = items.Count(item => item.Conflict is not null);
        operation.WarningItems = items.Count(item => item.Warnings.Count > 0);
        operation.PreviewedAt = now;
        operation.ExpiresAt = now + options.Value.PreviewLifetime;
        await repository.AddOperationAsync(operation, cancellationToken);
        return new OrganizePreviewResult(
            OrganizePreviewStatus.Created,
            await repository.FindOperationAsync(operation.Id, cancellationToken));
    }

    private DateTimeOffset Now()
    {
        return FileFingerprint.Truncate(timeProvider.GetUtcNow());
    }
}
