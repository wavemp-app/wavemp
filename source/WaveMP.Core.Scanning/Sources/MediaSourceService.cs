using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Metadata.Options;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Scanning.Jobs;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Watchers;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Sources;

internal sealed class MediaSourceService(
    IMediaSourceRepository repository,
    IMediaSourceCatalog catalog,
    IJobService jobs,
    IJobRunTracker tracker,
    MediaFolderWatcher watcher,
    LibraryWriteLock writeLock,
    IOptions<MediaLibraryOptions> mediaLibrary,
    TimeProvider timeProvider,
    ILogger<MediaSourceService> logger) : IMediaSourceService
{
    private const int NameMaxLength = 100;

    public async Task<IReadOnlyList<MediaSourceOverview>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, MediaSourceCounts> counts = await repository.CountAsync(cancellationToken);
        IReadOnlyList<MediaSource> sources = await repository.ListAsync(cancellationToken);
        return [.. sources.Select(source => ToOverview(source, counts))];
    }

    public async Task<MediaSourceOverview?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MediaSourceOverview> sources = await ListAsync(cancellationToken);
        return sources.FirstOrDefault(source => source.Id == id);
    }

    public async Task<MediaSourceResult> CreateAsync(
        CreateMediaSourceCommand command,
        CancellationToken cancellationToken = default)
    {
        if (NormalizeName(command.Name) is not { } name)
        {
            return MediaSourceResult.Failure(MediaSourceError.InvalidName);
        }

        (MediaSourceError? error, string? path) = await CheckPathAsync(command.Path, null, cancellationToken);
        if (error is { } pathError)
        {
            return MediaSourceResult.Failure(pathError);
        }

        string normalizedName = name.ToUpperInvariant();
        if (await repository.NameExistsAsync(normalizedName, cancellationToken: cancellationToken))
        {
            return MediaSourceResult.Failure(MediaSourceError.NameTaken);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        MediaSource source = new()
        {
            Id = Guid.CreateVersion7(now),
            Name = name,
            NormalizedName = normalizedName,
            Path = path!,
            IsActive = command.IsActive ?? true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        try
        {
            await repository.AddAsync(source, cancellationToken);
        }
        catch (MediaSourceNameConflictException)
        {
            return MediaSourceResult.Failure(MediaSourceError.NameTaken);
        }

        logger.LogInformation("Media source {Source} added with the folder {Folder}", source.Name, source.Path);
        await catalog.RefreshAsync(CancellationToken.None);
        if (source.IsActive)
        {
            await ScanAsync(source.Id);
        }

        return MediaSourceResult.Success((await GetAsync(source.Id, CancellationToken.None))!);
    }

    public async Task<MediaSourceResult> UpdateAsync(
        Guid id,
        UpdateMediaSourceCommand command,
        CancellationToken cancellationToken = default)
    {
        MediaSource? source = await repository.FindAsync(id, cancellationToken);
        if (source is null)
        {
            return MediaSourceResult.Failure(MediaSourceError.NotFound);
        }

        string? name = null;
        if (command.Name is not null && (name = NormalizeName(command.Name)) is null)
        {
            return MediaSourceResult.Failure(MediaSourceError.InvalidName);
        }

        string? path = null;
        if (command.Path is not null)
        {
            (MediaSourceError? error, string? checkedPath) = await CheckPathAsync(command.Path, id, cancellationToken);
            if (error is { } pathError)
            {
                return MediaSourceResult.Failure(pathError);
            }

            path = checkedPath;
        }

        bool pathChanged = path is not null
            && !string.Equals(path, source.Path ?? mediaLibrary.Value.RootPath, StringComparison.Ordinal);
        if (pathChanged && IsScanBusy())
        {
            return MediaSourceResult.Failure(MediaSourceError.ScanRunning);
        }

        if (name is not null)
        {
            string normalizedName = name.ToUpperInvariant();
            if (await repository.NameExistsAsync(normalizedName, id, cancellationToken))
            {
                return MediaSourceResult.Failure(MediaSourceError.NameTaken);
            }

            source.Name = name;
            source.NormalizedName = normalizedName;
        }

        bool activated = command.IsActive == true && !source.IsActive;
        if (path is not null)
        {
            source.Path = path;
        }

        if (command.IsActive is { } isActive)
        {
            source.IsActive = isActive;
        }

        source.UpdatedAt = timeProvider.GetUtcNow();
        try
        {
            await repository.UpdateAsync(source, cancellationToken);
        }
        catch (MediaSourceNameConflictException)
        {
            return MediaSourceResult.Failure(MediaSourceError.NameTaken);
        }

        logger.LogInformation(
            "Media source {Source} changed: folder {Folder}, active {IsActive}",
            source.Name,
            source.Path,
            source.IsActive);
        await catalog.RefreshAsync(CancellationToken.None);
        if (source.IsActive && (activated || pathChanged))
        {
            await ScanAsync(source.Id);
        }

        return MediaSourceResult.Success((await GetAsync(source.Id, CancellationToken.None))!);
    }

    public async Task<MediaSourceError?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await repository.FindAsync(id, cancellationToken) is null)
        {
            return MediaSourceError.NotFound;
        }

        if (IsScanBusy())
        {
            return MediaSourceError.ScanRunning;
        }

        bool deleted;
        using (await writeLock.AcquireAsync(cancellationToken))
        {
            deleted = await repository.DeleteAsync(id, cancellationToken);
        }

        if (!deleted)
        {
            return MediaSourceError.NotFound;
        }

        logger.LogInformation("Media source {SourceId} removed with its tracks", id);
        await catalog.RefreshAsync(CancellationToken.None);
        return null;
    }

    /// <summary>
    /// The trimmed name, or <c>null</c> if it is empty or too long.
    /// </summary>
    internal static string? NormalizeName(string? value)
    {
        string name = value?.Trim() ?? string.Empty;
        return name.Length is > 0 and <= NameMaxLength ? name : null;
    }

    /// <summary>
    /// Checks a new folder: absolute, existing, and apart from the folders of the other sources.
    /// </summary>
    private async Task<(MediaSourceError? Error, string? Path)> CheckPathAsync(
        string? value,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        if (!MediaSourcePaths.TryNormalize(value, out string path))
        {
            return (MediaSourceError.InvalidPath, null);
        }

        if (!Directory.Exists(path))
        {
            return (MediaSourceError.PathNotFound, null);
        }

        await catalog.RefreshAsync(cancellationToken);
        bool overlaps = catalog.Sources
            .Where(source => source.Id != exceptId)
            .Any(source => MediaSourcePaths.Overlap(path, source.RootPath));
        return overlaps ? (MediaSourceError.PathOverlaps, null) : (null, path);
    }

    private bool IsScanBusy()
    {
        return tracker.GetState(JobIds.LibraryScan) != JobActivityState.Idle;
    }

    /// <summary>
    /// Scans the whole source now, or once the running scan is done.
    /// </summary>
    private async Task ScanAsync(Guid sourceId)
    {
        try
        {
            JobRunStartResult result = await jobs.StartRunAsync(
                new JobRunRequest(
                    JobIds.LibraryScan,
                    JobRunTrigger.Manual,
                    LibraryScanJob.CreatePayload(ScanScope.OfSources([sourceId]))),
                CancellationToken.None);
            if (result.IsSuccess)
            {
                return;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot start the scan of the media source {SourceId}", sourceId);
        }

        watcher.QueueSource(sourceId);
    }

    private MediaSourceOverview ToOverview(MediaSource source, IReadOnlyDictionary<Guid, MediaSourceCounts> counts)
    {
        string path = source.Path ?? mediaLibrary.Value.RootPath;
        MediaSourceCounts count = counts.GetValueOrDefault(source.Id) ?? new MediaSourceCounts(0, 0);
        return new MediaSourceOverview(
            source.Id,
            source.Name,
            path,
            source.Path is null,
            source.IsActive,
            Directory.Exists(path),
            watcher.IsWatchingSource(source.Id),
            count.Tracks,
            count.Files,
            source.CreatedAt,
            source.UpdatedAt);
    }
}
