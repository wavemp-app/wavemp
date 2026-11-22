using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Metadata.Probes;
using WaveMP.Core.Metadata.Readers;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Scanning.Discoveries;
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
/// <see cref="ScannerVersions.AlbumGrouping"/>) and cleans up after that, checks ffprobe and the folder of every source
/// of the scope, walks the folders of the scope, compares them with the library, reads new and changed files in
/// parallel, writes every changed folder at once, removes folders that are gone and finally updates album covers,
/// removes artists and albums without tracks and counts the library. A source whose folder is missing or unreadable,
/// or whose full scan finds no files at all while the library has files of it, is skipped with an issue and its
/// library stays unchanged, so an unmounted folder never empties the library; if every source of the scan is skipped,
/// the scan fails. Reading and writing a folder and the clean-up hold the <see cref="LibraryWriteLock"/>, so other
/// writers wait at most for one folder.
/// </summary>
internal sealed class LibraryScanner(
    ILibraryScanRepository library,
    LibraryWriter writer,
    LibraryWriteLock writeLock,
    IMediaMetadataReader reader,
    IMediaProber prober,
    ISettingsSnapshot<LibrarySettings> settings,
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
            return await RunAsync(scope, cancellationToken);
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
            cancellationToken);
        await library.DeleteOrphansAsync(timeProvider.GetUtcNow(), cancellationToken);
        await writer.UpdateAlbumCoversAsync(librarySettings.AlbumCoverNames, cancellationToken);
        await writer.UpdateAlbumCompilationsAsync(timeProvider.GetUtcNow(), cancellationToken);

        foreach (ScanIssue issue in issues.Listed)
        {
            logger.LogWarning("The file {File} has a problem: {Issue}", issue.Path, issue.Code);
        }

        logger.LogInformation(
            "Folder {Folder} of the media source {Source} read again: {FilesRead} files read, {FilesRemoved} removed; "
            + "{TracksAdded} tracks added, {TracksUpdated} updated, {TracksRemoved} removed",
            directory,
            source.Name,
            changed.FilesToRead.Count,
            changed.Removed,
            written.TracksAdded,
            written.TracksUpdated,
            written.TracksRemoved);
        return true;
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
        }

        // A scan that could scan none of its sources fails with the reason of the first one.
        if (failures.Count > 0 && plans.Count == 0)
        {
            throw failures[0];
        }

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
                        cancellationToken);
                }

                tracksAdded += written.TracksAdded;
                tracksUpdated += written.TracksUpdated;
                tracksRemoved += written.TracksRemoved;
                progress.DirectoryProcessed();
            }
        }

        progress.CleaningUp();
        bool changed = tracksRegrouped > 0 || plans.Any(item =>
            item.Plan.ChangedDirectories.Count > 0 || item.Plan.DeletedDirectories.Count > 0);
        using (await writeLock.AcquireAsync(cancellationToken))
        {
            // The changed folders are written above, so a renamed or moved folder is found in its new place here and
            // the data of its album follows the songs.
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

            // A full scan always updates the album covers and compilations, so changed cover names apply without
            // changed files.
            if (changed || scope.IsFull)
            {
                await writer.UpdateAlbumCoversAsync(librarySettings.AlbumCoverNames, cancellationToken);
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
            + "{FilesFailed} with problems; {TracksTotal} tracks",
            statistics.FilesFound,
            statistics.FilesAdded,
            statistics.FilesUpdated,
            statistics.FilesRemoved,
            statistics.FilesFailed,
            statistics.TracksTotal);
        return new ScanResult(statistics, issues.Listed);
    }

    /// <summary>
    /// Forms the albums of every folder with tracks again from the stored files, without reading a file, for all
    /// sources (also deactivated ones) whose albums were formed with an older version of the rules
    /// (<see cref="ScannerVersions.AlbumGrouping"/>). Every folder is written on its own while holding the
    /// <see cref="LibraryWriteLock"/>. Then removes albums and artists without tracks, updates album covers and
    /// compilations and only then stores the version: the rest of the scan may fail (e.g. a folder that is not
    /// mounted), and an interrupted run is repeated as a whole by the next scan. Returns the number of tracks that
    /// changed.
    /// </summary>
    private async Task<int> RegroupAsync(LibrarySettings librarySettings, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> sourceIds = await library.GetSourcesToRegroupAsync(
            ScannerVersions.AlbumGrouping,
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
                + "{Version}",
                sourceId,
                directories.Count,
                ScannerVersions.AlbumGrouping);
        }

        using (await writeLock.AcquireAsync(cancellationToken))
        {
            await library.DeleteOrphansAsync(timeProvider.GetUtcNow(), cancellationToken);
            await writer.UpdateAlbumCoversAsync(librarySettings.AlbumCoverNames, cancellationToken);
            await writer.UpdateAlbumCompilationsAsync(timeProvider.GetUtcNow(), cancellationToken);
            foreach (Guid sourceId in sourceIds)
            {
                await library.SetAlbumGroupingVersionAsync(sourceId, ScannerVersions.AlbumGrouping, cancellationToken);
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
