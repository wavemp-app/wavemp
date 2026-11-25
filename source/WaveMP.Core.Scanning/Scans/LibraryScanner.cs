using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Metadata.Probes;
using WaveMP.Core.Metadata.Readers;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Scanning.Discoveries;
using WaveMP.Core.Scanning.Moves;
using WaveMP.Core.Scanning.Options;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Scans;

/// <summary>
/// Scans the media sources: forms the albums of sources again whose albums follow older rules (see
/// <see cref="ScannerVersions.AlbumGrouping"/>) or whose titles were cleaned in another way than the layout template
/// asks for now (see <see cref="TitleCleaningModes"/>) and cleans up after that, checks ffprobe and the folder of every
/// source of the scope, walks the folders of the scope, compares them with the library, finds the songs whose folder
/// was renamed or moved or whose files were renamed within the scan (see <see cref="TrackMovePlanner"/>), reads new and
/// changed files in parallel, writes every changed folder at once (keeping those songs with their id and all their data
/// at their new place), removes the vanished songs no folder kept and the folders that are gone and finally updates
/// album covers, removes artists and albums without tracks and counts the library. A source whose folder is missing or
/// unreadable, or whose full scan finds no files at all while the library has files of it, is skipped with an issue and
/// its library stays unchanged, so an unmounted folder never empties the library; if every source of the scan is
/// skipped, the scan fails. Reading and writing a folder and the clean-up hold the <see cref="LibraryWriteLock"/>, so
/// other writers wait at most for one folder. The whole run holds the <see cref="LibraryMaintenanceLock"/>, so it waits
/// for a running organization of the media files and such a run waits for the scan.
/// </summary>
internal sealed class LibraryScanner(
    ILibraryScanRepository library,
    LibraryWriter writer,
    LibraryWriteLock writeLock,
    LibraryMaintenanceLock maintenanceLock,
    IMediaMetadataReader reader,
    IMediaProber prober,
    ISettingsSnapshot<LibrarySettings> settings,
    ISettingsSnapshot<OrganizingSettings> organizing,
    IScanProgressTracker progress,
    IMediaSourceCatalog sources,
    IOptions<ScanOptions> options,
    TimeProvider timeProvider,
    ILogger<LibraryScanner> logger) : ILibraryScanner
{
    public async Task<ScanResult> ScanAsync(
        ScanScope scope,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        progress.Start(runId);
        try
        {
            if (maintenanceLock.IsHeld)
            {
                logger.LogInformation("The scan waits until the files are no longer being organized");
            }

            using (await maintenanceLock.AcquireAsync(cancellationToken))
            {
                return await RunAsync(scope, cancellationToken);
            }
        }
        finally
        {
            progress.Complete(runId);
        }
    }

    public async Task<bool> RefreshFolderAsync(
        Guid sourceId,
        string directoryPath,
        IReadOnlyCollection<string> changedFiles,
        CancellationToken cancellationToken = default)
    {
        string directory = ScanScope.Normalize(directoryPath);
        ScanOptions scanOptions = options.Value;
        LibrarySettings librarySettings = settings.Current;
        if (sources.Find(sourceId) is not { } source)
        {
            logger.LogWarning("The media source {SourceId} does not exist; nothing was read again", sourceId);
            return false;
        }

        IssueCollector issues = new(scanOptions.MaxIssuesPerScan);
        string root = source.RootPath;

        using IDisposable _ = await writeLock.AcquireAsync(cancellationToken);
        if (!Directory.Exists(root))
        {
            logger.LogWarning("The folder {Folder} of the media source does not exist; nothing was read again", root);
            return false;
        }

        DiscoveredDirectory? discovered = MediaFolderWalker.WalkFolder(
            root,
            directory,
            new MediaKindClassifier(librarySettings),
            scanOptions.MaxPathBytes,
            path => issues.Add(source, path, ScanIssueCode.PathTooLong),
            cancellationToken);
        if (discovered is null)
        {
            logger.LogWarning("The folder {Folder} does not exist; nothing was read again", directory);
            return false;
        }

        IReadOnlyList<MediaFileSnapshot> stored = await library.GetFolderSnapshotsAsync(
            sourceId,
            directory,
            cancellationToken);
        ScanPlan plan = ScanPlanner.Plan(
            [discovered],
            stored,
            ScannerVersions.MetadataOf,
            changedFiles.ToHashSet(StringComparer.Ordinal));
        if (plan.ChangedDirectories is not [DirectoryPlan changed])
        {
            return false;
        }

        // Finds the songs of the folder whose files were renamed.
        TrackMoves moves = TrackMovePlanner.Plan([(sourceId, plan, stored)]);
        IReadOnlyDictionary<string, MediaFileMetadata> read = await ReadAsync(
            source,
            changed.FilesToRead,
            scanOptions.MetadataParallelism,
            issues,
            null,
            cancellationToken);
        FolderWriteResult written = await writer.WriteFolderAsync(
            sourceId,
            changed,
            read,
            timeProvider.GetUtcNow(),
            moves,
            cancellationToken);
        int vanished = await library.DeleteVanishedTracksAsync(
            [.. moves.Deferred.Where(track => !written.AdoptedTrackIds.Contains(track.TrackId))],
            cancellationToken);
        await library.DeleteOrphansAsync(timeProvider.GetUtcNow(), cancellationToken);
        await writer.UpdateAlbumCoversAsync(librarySettings.AlbumCoverNames, cancellationToken);
        await writer.UpdateArtistPicturesAsync(librarySettings.ArtistPictureNames, cancellationToken);
        await writer.UpdateAlbumCompilationsAsync(timeProvider.GetUtcNow(), cancellationToken);

        foreach (ScanIssue issue in issues.Listed)
        {
            logger.LogWarning("The file {File} has a problem: {Issue}", issue.Path, issue.Code);
        }

        logger.LogInformation(
            "Folder {Folder} of the media source {Source} read again: {FilesRead} files read, {FilesRemoved} removed; "
            + "{TracksAdded} tracks added, {TracksUpdated} updated ({TracksMoved} of them renamed), "
            + "{TracksRemoved} removed",
            directory,
            source.Name,
            changed.FilesToRead.Count,
            changed.Removed,
            written.TracksAdded,
            written.TracksUpdated,
            written.AdoptedTrackIds.Count,
            written.TracksRemoved + vanished);
        return true;
    }

    public async Task<int> RefreshFoldersAsync(
        IReadOnlyCollection<FolderRefresh> folders,
        CancellationToken cancellationToken = default)
    {
        ScanOptions scanOptions = options.Value;
        LibrarySettings librarySettings = settings.Current;
        IssueCollector issues = new(scanOptions.MaxIssuesPerScan);
        int refreshed = 0;
        foreach (FolderRefresh folder in folders)
        {
            if (sources.Find(folder.SourceId) is not { } source)
            {
                continue;
            }

            string directory = ScanScope.Normalize(folder.DirectoryPath);
            using (await writeLock.AcquireAsync(cancellationToken))
            {
                if (!Directory.Exists(source.RootPath))
                {
                    continue;
                }

                DiscoveredDirectory? discovered = MediaFolderWalker.WalkFolder(
                    source.RootPath,
                    directory,
                    new MediaKindClassifier(librarySettings),
                    scanOptions.MaxPathBytes,
                    path => issues.Add(source, path, ScanIssueCode.PathTooLong),
                    cancellationToken);
                if (discovered is null)
                {
                    continue;
                }

                IReadOnlyList<MediaFileSnapshot> stored = await library.GetFolderSnapshotsAsync(
                    source.Id,
                    directory,
                    cancellationToken);
                ScanPlan plan = ScanPlanner.Plan(
                    [discovered],
                    stored,
                    ScannerVersions.MetadataOf,
                    folder.ChangedFiles.ToHashSet(StringComparer.Ordinal));
                if (plan.ChangedDirectories is [DirectoryPlan changed])
                {
                    TrackMoves moves = TrackMovePlanner.Plan([(source.Id, plan, stored)]);
                    IReadOnlyDictionary<string, MediaFileMetadata> read = await ReadAsync(
                        source,
                        changed.FilesToRead,
                        scanOptions.MetadataParallelism,
                        issues,
                        null,
                        cancellationToken);
                    FolderWriteResult written = await writer.WriteFolderAsync(
                        source.Id,
                        changed,
                        read,
                        timeProvider.GetUtcNow(),
                        moves,
                        cancellationToken);
                    await library.DeleteVanishedTracksAsync(
                        [.. moves.Deferred.Where(track => !written.AdoptedTrackIds.Contains(track.TrackId))],
                        cancellationToken);
                }
                else if (stored.Count > 0)
                {
                    // Nothing new on disk, but the songs are at a new place: albums known by their folder and metadata
                    // taken from the path follow it.
                    await writer.RewriteFolderAsync(source.Id, directory, timeProvider.GetUtcNow(), cancellationToken);
                }

                refreshed++;
            }
        }

        using (await writeLock.AcquireAsync(cancellationToken))
        {
            await library.DeleteOrphansAsync(timeProvider.GetUtcNow(), cancellationToken);
            await writer.UpdateAlbumCoversAsync(librarySettings.AlbumCoverNames, cancellationToken);
            await writer.UpdateArtistPicturesAsync(librarySettings.ArtistPictureNames, cancellationToken);
            await writer.UpdateAlbumCompilationsAsync(timeProvider.GetUtcNow(), cancellationToken);
        }

        foreach (ScanIssue issue in issues.Listed)
        {
            logger.LogWarning("The file {File} has a problem: {Issue}", issue.Path, issue.Code);
        }

        logger.LogInformation("{Folders} folders read again after their files were moved", refreshed);
        return refreshed;
    }

    private async Task<ScanResult> RunAsync(ScanScope scope, CancellationToken cancellationToken)
    {
        ScanOptions scanOptions = options.Value;
        LibrarySettings librarySettings = settings.Current;
        IssueCollector issues = new(scanOptions.MaxIssuesPerScan);
        int tracksRegrouped = await RegroupAsync(librarySettings, cancellationToken);

        MediaSourceInfo[] targets = [.. sources.Sources
            .Where(source => source.IsActive && scope.DirectoriesOf(source.Id) is not null)];
        if (scope.IsFull && targets.Length == 0)
        {
            throw new JobFailedException(JobErrorCode.NoMediaSources, "There is no active media source to scan.");
        }

        if (!await prober.IsAvailableAsync(cancellationToken))
        {
            throw new JobFailedException(JobErrorCode.ProbeUnavailable, "ffprobe cannot be started.");
        }

        MediaKindClassifier classifier = new(librarySettings);
        List<(MediaSourceInfo Source, ScanPlan Plan)> plans = [];
        List<(Guid SourceId, ScanPlan Plan, IReadOnlyList<MediaFileSnapshot> Stored)> compared = [];
        List<JobFailedException> failures = [];
        int directoriesFound = 0;
        int filesFound = 0;
        foreach (MediaSourceInfo source in targets)
        {
            IReadOnlyList<string> directories = scope.DirectoriesOf(source.Id)!;
            bool isWholeSource = directories is [{ Length: 0 }];
            try
            {
                CheckSourceFolder(source);
            }
            catch (JobFailedException exception)
            {
                failures.Add(exception);
                issues.Add(source, string.Empty, IssueOf(exception.Code));
                logger.LogWarning("The media source {Source} is skipped: {Reason}", source.Name, exception.Message);
                continue;
            }

            logger.LogInformation(
                "Scanning {Scope} of the media source {Source} in {Folder}",
                isWholeSource ? "all folders" : string.Join(", ", directories),
                source.Name,
                source.RootPath);

            List<DiscoveredDirectory> discovered = [];
            foreach (string directory in directories)
            {
                discovered.AddRange(MediaFolderWalker.Walk(
                    source.RootPath,
                    directory,
                    classifier,
                    scanOptions.MaxPathBytes,
                    path => issues.Add(source, path, ScanIssueCode.PathTooLong),
                    cancellationToken));
                progress.Discovered(
                    directoriesFound + discovered.Count,
                    filesFound + discovered.Sum(item => item.Files.Count));
            }

            IReadOnlyList<MediaFileSnapshot> stored = await library.GetFileSnapshotsAsync(
                source.Id,
                isWholeSource ? null : directories,
                cancellationToken);
            ScanPlan plan = ScanPlanner.Plan(discovered, stored, ScannerVersions.MetadataOf);
            if (isWholeSource && plan.FilesFound == 0 && stored.Count > 0)
            {
                JobFailedException empty = new(
                    JobErrorCode.MediaFolderEmpty,
                    $"The folder {source.RootPath} of the media source {source.Name} has no media files, but the "
                    + $"library has {stored.Count}. Nothing was removed; check that the folder is mounted.");
                failures.Add(empty);
                issues.Add(source, string.Empty, ScanIssueCode.SourceEmpty);
                logger.LogWarning("The media source {Source} is skipped: {Reason}", source.Name, empty.Message);
                continue;
            }

            directoriesFound += plan.DirectoriesFound;
            filesFound += plan.FilesFound;
            plans.Add((source, plan));
            compared.Add((source.Id, plan, stored));
        }

        // A scan that could scan none of its sources fails with the reason of the first one.
        if (failures.Count > 0 && plans.Count == 0)
        {
            throw failures[0];
        }

        // Found before any folder is written, so a song is kept wherever it went within the scan and in whatever order
        // its old and its new folder are written.
        TrackMoves moves = TrackMovePlanner.Plan(compared);
        compared.Clear();
        HashSet<Guid> adopted = [];

        progress.Planned(
            plans.Sum(item => item.Plan.ChangedDirectories.Count),
            plans.Sum(item => item.Plan.ChangedDirectories.Sum(directory => directory.FilesToRead.Count)));

        int tracksAdded = 0;
        int tracksUpdated = tracksRegrouped;
        int tracksRemoved = 0;
        foreach ((MediaSourceInfo source, ScanPlan plan) in plans)
        {
            foreach (DirectoryPlan directory in plan.ChangedDirectories)
            {
                FolderWriteResult written;
                using (await writeLock.AcquireAsync(cancellationToken))
                {
                    IReadOnlyDictionary<string, MediaFileMetadata> read;
                    try
                    {
                        read = await ReadAsync(
                            source,
                            directory.FilesToRead,
                            scanOptions.MetadataParallelism,
                            issues,
                            progress,
                            cancellationToken);
                    }
                    catch (ProbeUnavailableException exception)
                    {
                        throw new JobFailedException(JobErrorCode.ProbeUnavailable, exception.Message, exception);
                    }

                    written = await writer.WriteFolderAsync(
                        source.Id,
                        directory,
                        read,
                        timeProvider.GetUtcNow(),
                        moves,
                        cancellationToken);
                }

                tracksAdded += written.TracksAdded;
                tracksUpdated += written.TracksUpdated;
                tracksRemoved += written.TracksRemoved;
                adopted.UnionWith(written.AdoptedTrackIds);
                progress.DirectoryProcessed();
            }
        }

        progress.CleaningUp();
        bool changed = tracksRegrouped > 0 || plans.Any(item =>
            item.Plan.ChangedDirectories.Count > 0 || item.Plan.DeletedDirectories.Count > 0);
        using (await writeLock.AcquireAsync(cancellationToken))
        {
            // The vanished songs no folder kept go now. Those of deleted folders go with their folder below, which
            // lets the data of their album follow them as before.
            HashSet<(Guid SourceId, string DirectoryPath)> deleted = [.. plans.SelectMany(item =>
                item.Plan.DeletedDirectories.Select(directory => (item.Source.Id, directory)))];
            tracksRemoved += await library.DeleteVanishedTracksAsync(
                [.. moves.Deferred.Where(track => !adopted.Contains(track.TrackId)
                    && !deleted.Contains((track.SourceId, track.DirectoryPath)))],
                cancellationToken);

            // The changed folders are written above, so a renamed or moved folder is found in its new place here and
            // the data of its album follows the songs; songs kept at their new place have left it already.
            foreach ((MediaSourceInfo source, ScanPlan plan) in plans)
            {
                (int _, int deletedTracks) = await library.DeleteDirectoriesAsync(
                    source.Id,
                    plan.DeletedDirectories,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
                tracksRemoved += deletedTracks;
            }

            if (changed)
            {
                await library.DeleteOrphansAsync(timeProvider.GetUtcNow(), cancellationToken);
            }

            // A full scan always updates the album covers, the artist pictures and the compilations, so changed
            // picture names apply without changed files.
            if (changed || scope.IsFull)
            {
                await writer.UpdateAlbumCoversAsync(librarySettings.AlbumCoverNames, cancellationToken);
                await writer.UpdateArtistPicturesAsync(librarySettings.ArtistPictureNames, cancellationToken);
                await writer.UpdateAlbumCompilationsAsync(timeProvider.GetUtcNow(), cancellationToken);
            }
        }

        LibraryTotals totals = await library.CountLibraryAsync(cancellationToken);
        ScanPlan[] all = [.. plans.Select(item => item.Plan)];
        ScanStatistics statistics = new()
        {
            DirectoriesScanned = all.Sum(plan => plan.DirectoriesFound),
            DirectoriesChanged = all.Sum(plan => plan.ChangedDirectories.Count + plan.DeletedDirectories.Count),
            FilesFound = all.Sum(plan => plan.FilesFound),
            FilesAdded = all.Sum(plan => plan.ChangedDirectories.Sum(directory => directory.Added)),
            FilesUpdated = all.Sum(plan => plan.ChangedDirectories.Sum(directory => directory.Updated)),
            FilesRemoved = all.Sum(plan =>
                plan.ChangedDirectories.Sum(directory => directory.Removed) + plan.FilesInDeletedDirectories),
            FilesUnchanged = all.Sum(plan => plan.FilesUnchanged),
            FilesFailed = issues.Count,
            TracksAdded = tracksAdded,
            TracksUpdated = tracksUpdated,
            TracksRemoved = tracksRemoved,
            TracksTotal = totals.Tracks,
            AlbumsTotal = totals.Albums,
            ArtistsTotal = totals.Artists,
            IsPartial = !scope.IsFull,
        };

        logger.LogInformation(
            "Scan finished: {FilesFound} files, {FilesAdded} added, {FilesUpdated} updated, {FilesRemoved} removed, "
            + "{FilesFailed} with problems; {TracksTotal} tracks, {TracksMoved} of them kept at a new place",
            statistics.FilesFound,
            statistics.FilesAdded,
            statistics.FilesUpdated,
            statistics.FilesRemoved,
            statistics.FilesFailed,
            statistics.TracksTotal,
            adopted.Count);
        return new ScanResult(statistics, issues.Listed);
    }

    /// <summary>
    /// Forms the albums of every folder with tracks again from the stored files, without reading a file, for all
    /// sources (also deactivated ones) whose albums were formed with an older version of the rules
    /// (<see cref="ScannerVersions.AlbumGrouping"/>) or whose titles were cleaned with another mode than the current
    /// one (<see cref="TitleCleaningModes"/>). Every folder is written on its own while holding the
    /// <see cref="LibraryWriteLock"/>. Then removes albums and artists without tracks, updates album covers and
    /// compilations and only then stores the version: the rest of the scan may fail (e.g. a folder that is not
    /// mounted), and an interrupted run is repeated as a whole by the next scan. The mode is taken once; a change of
    /// the template during the run leaves the old mode stored, so the next scan forms the folders again. Returns the
    /// number of tracks that changed.
    /// </summary>
    private async Task<int> RegroupAsync(LibrarySettings librarySettings, CancellationToken cancellationToken)
    {
        TitleCleaningMode titleCleaning = TitleCleaningModes.Of(organizing.Current);
        IReadOnlyList<Guid> sourceIds = await library.GetSourcesToRegroupAsync(
            ScannerVersions.AlbumGrouping,
            titleCleaning,
            cancellationToken);
        if (sourceIds.Count == 0)
        {
            return 0;
        }

        int changed = 0;
        foreach (Guid sourceId in sourceIds)
        {
            IReadOnlyList<string> directories = await library.GetTrackDirectoriesAsync(sourceId, cancellationToken);
            foreach (string directory in directories)
            {
                using (await writeLock.AcquireAsync(cancellationToken))
                {
                    FolderWriteResult written = await writer.RewriteFolderAsync(
                        sourceId,
                        directory,
                        timeProvider.GetUtcNow(),
                        cancellationToken);
                    changed += written.TracksUpdated;
                }
            }

            logger.LogInformation(
                "Albums of the media source {SourceId} formed again in {Folders} folders with the rules of version "
                + "{Version} and the title cleaning {TitleCleaning}",
                sourceId,
                directories.Count,
                ScannerVersions.AlbumGrouping,
                titleCleaning);
        }

        using (await writeLock.AcquireAsync(cancellationToken))
        {
            await library.DeleteOrphansAsync(timeProvider.GetUtcNow(), cancellationToken);
            await writer.UpdateAlbumCoversAsync(librarySettings.AlbumCoverNames, cancellationToken);
            await writer.UpdateArtistPicturesAsync(librarySettings.ArtistPictureNames, cancellationToken);
            await writer.UpdateAlbumCompilationsAsync(timeProvider.GetUtcNow(), cancellationToken);
            foreach (Guid sourceId in sourceIds)
            {
                await library.SetAlbumGroupingVersionAsync(
                    sourceId,
                    ScannerVersions.AlbumGrouping,
                    titleCleaning,
                    cancellationToken);
            }
        }

        return changed;
    }

    private static void CheckSourceFolder(MediaSourceInfo source)
    {
        string root = source.RootPath;
        if (!Directory.Exists(root))
        {
            throw new JobFailedException(
                JobErrorCode.MediaFolderNotFound,
                $"The folder {root} of the media source {source.Name} does not exist.");
        }

        try
        {
            using IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(root).GetEnumerator();
            entries.MoveNext();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new JobFailedException(
                JobErrorCode.MediaFolderUnreadable,
                $"The folder {root} of the media source {source.Name} cannot be read.",
                exception);
        }
    }

    private static ScanIssueCode IssueOf(JobErrorCode code)
    {
        return code == JobErrorCode.MediaFolderUnreadable
            ? ScanIssueCode.SourceUnreadable
            : ScanIssueCode.SourceNotFound;
    }

    /// <summary>
    /// Reads the files of a source in parallel. Problems of files are collected; <paramref name="progressTracker"/>
    /// (the progress of a scan run) learns about every file read.
    /// </summary>
    /// <exception cref="ProbeUnavailableException">ffprobe cannot be started.</exception>
    private async Task<IReadOnlyDictionary<string, MediaFileMetadata>> ReadAsync(
        MediaSourceInfo source,
        IReadOnlyList<DiscoveredFile> files,
        int parallelism,
        IssueCollector issues,
        IScanProgressTracker? progressTracker,
        CancellationToken cancellationToken)
    {
        ConcurrentDictionary<string, MediaFileMetadata> read = new(StringComparer.Ordinal);
        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = Math.Max(1, parallelism),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(files, parallelOptions, async (file, token) =>
        {
            string path = Path.Combine(source.RootPath, MediaFolderWalker.ToSystemPath(file.RelativePath));
            MediaFileMetadata metadata = await reader.ReadAsync(path, file.Kind, token);
            if (metadata.Issue is { } issue)
            {
                issues.Add(source, file.RelativePath, issue switch
                {
                    MediaFileIssue.TagsUnreadable => ScanIssueCode.TagsUnreadable,
                    MediaFileIssue.ProbeFailed => ScanIssueCode.ProbeFailed,
                    _ => ScanIssueCode.FileUnreadable,
                });
            }

            read[file.RelativePath] = metadata;
            progressTracker?.FileRead();
        });

        return read;
    }

    /// <summary>
    /// Counts all problems and keeps the first ones for the result.
    /// </summary>
    private sealed class IssueCollector(int maxListed)
    {
        private readonly Lock _lock = new();
        private readonly List<ScanIssue> _listed = [];

        public int Count { get; private set; }

        public IReadOnlyList<ScanIssue> Listed
        {
            get
            {
                lock (_lock)
                {
                    return [.. _listed
                        .OrderBy(issue => issue.SourceName, StringComparer.Ordinal)
                        .ThenBy(issue => issue.Path, StringComparer.Ordinal)];
                }
            }
        }

        public void Add(MediaSourceInfo source, string path, ScanIssueCode code)
        {
            lock (_lock)
            {
                Count++;
                if (_listed.Count < maxListed)
                {
                    _listed.Add(new ScanIssue(path, code, source.Id, source.Name));
                }
            }
        }
    }
}
