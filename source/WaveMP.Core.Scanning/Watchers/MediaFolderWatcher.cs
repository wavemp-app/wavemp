using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Scanning.Discoveries;
using WaveMP.Core.Scanning.Jobs;
using WaveMP.Core.Scanning.Options;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Watchers;

/// <summary>
/// Watches the folders of the active media sources for new, changed and deleted media files and scans the changed
/// folders shortly after (debounced). File system events are unreliable on network shares, so the periodic scan always
/// stays active. While a scan runs, the changes are kept and scanned afterwards. Switched on and off by the library
/// settings; follows the changes of the sources. Also scans sources the admin added or changed once no scan runs
/// (<see cref="QueueSource"/>), whether the watcher is on or not.
/// </summary>
public sealed class MediaFolderWatcher(
    IMediaSourceCatalog sources,
    IOptions<ScanOptions> options,
    ISettingsSnapshot<LibrarySettings> settings,
    IServiceScopeFactory scopeFactory,
    IJobRunTracker tracker,
    TimeProvider timeProvider,
    ILogger<MediaFolderWatcher> logger) : IHostedService, ISettingsChangeHandler<LibrarySettings>, IDisposable
{
    private readonly Lock _lock = new();
    private readonly MediaChangeBatch _batch = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Dictionary<Guid, FileSystemWatcher> _watchers = [];
    private bool _enabled;
    private Task? _loop;

    /// <summary>
    /// Whether the watcher watches the folder of at least one source.
    /// </summary>
    public bool IsWatching
    {
        get
        {
            lock (_lock)
            {
                return _watchers.Count > 0;
            }
        }
    }

    /// <summary>
    /// Whether the watcher watches the folder of the source.
    /// </summary>
    public bool IsWatchingSource(Guid sourceId)
    {
        lock (_lock)
        {
            return _watchers.ContainsKey(sourceId);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        sources.Changed += OnSourcesChanged;
        if (settings.Current.FileWatcherEnabled)
        {
            Enable();
        }

        _loop = Task.Run(() => RunAsync(_stopping.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        sources.Changed -= OnSourcesChanged;
        Disable();
        await _stopping.CancelAsync();
        if (_loop is not null)
        {
            await _loop.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    public ValueTask OnSettingsChangedAsync(
        LibrarySettings previous,
        LibrarySettings current,
        CancellationToken cancellationToken)
    {
        if (current.FileWatcherEnabled && !_enabled)
        {
            Enable();
        }
        else if (!current.FileWatcherEnabled && _enabled)
        {
            Disable();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Scans a whole source as soon as no scan runs, e.g. after the admin added it and a scan was already running.
    /// </summary>
    public void QueueSource(Guid sourceId)
    {
        lock (_lock)
        {
            _batch.RequestSource(sourceId, timeProvider.GetUtcNow());
        }
    }

    /// <summary>
    /// Stops watching and ends the scan loop. Like <see cref="BackgroundService"/>, this only cancels the
    /// <see cref="CancellationTokenSource"/> and does not dispose it: the host can stop its services twice, and the
    /// second stop may run after the first one disposed them (the test host stops the host while the
    /// <c>RunAsync</c> of the program stops and disposes it as well). A source without a timer holds no resources.
    /// </summary>
    public void Dispose()
    {
        sources.Changed -= OnSourcesChanged;
        Disable();
        _stopping.Cancel();
    }

    private void OnSourcesChanged(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (!_enabled)
            {
                return;
            }
        }

        // Watches the active sources again: new and changed folders, none of deactivated or removed sources.
        Enable();
    }

    private void Enable()
    {
        IReadOnlyList<MediaSourceInfo> active;
        try
        {
            active = [.. sources.Sources.Where(source => source.IsActive)];
        }
        catch (Exception exception)
        {
            // E.g. without a database while the OpenAPI document is generated at build time.
            logger.LogWarning(
                "The media sources cannot be loaded, the file watcher is off: {Reason}",
                exception.Message);
            return;
        }

        List<(Guid Id, FileSystemWatcher Watcher)> created = [];
        foreach (MediaSourceInfo source in active)
        {
            if (Watch(source) is { } watcher)
            {
                created.Add((source.Id, watcher));
            }
        }

        lock (_lock)
        {
            DisposeWatchers();
            foreach ((Guid id, FileSystemWatcher watcher) in created)
            {
                _watchers[id] = watcher;
            }

            _enabled = true;
            _batch.Keep(active.Select(source => source.Id).ToHashSet());
        }
    }

    private FileSystemWatcher? Watch(MediaSourceInfo source)
    {
        string root = source.RootPath;
        if (!Directory.Exists(root))
        {
            logger.LogWarning(
                "The folder {Folder} of the media source {Source} does not exist; it is not watched",
                root,
                source.Name);
            return null;
        }

        try
        {
            FileSystemWatcher watcher = new(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
            };
            watcher.Created += (_, change) => OnChanged(source, change.FullPath, change.ChangeType);
            watcher.Changed += (_, change) => OnChanged(source, change.FullPath, change.ChangeType);
            watcher.Deleted += (_, change) => OnChanged(source, change.FullPath, change.ChangeType);
            watcher.Renamed += (_, change) =>
            {
                OnChanged(source, change.OldFullPath, WatcherChangeTypes.Deleted);
                OnChanged(source, change.FullPath, WatcherChangeTypes.Created);
            };
            watcher.Error += (_, error) => OnError(source, error.GetException());
            watcher.EnableRaisingEvents = true;
            logger.LogInformation(
                "Watching the folder {Folder} of the media source {Source} for changes",
                root,
                source.Name);
            return watcher;
        }
        catch (Exception exception) when (exception is IOException
            or ArgumentException
            or UnauthorizedAccessException
            or PlatformNotSupportedException)
        {
            // E.g. the inotify limits of Linux; the periodic scan still finds the changes.
            logger.LogWarning(
                exception,
                "The file watcher cannot watch {Folder} of the media source {Source}; it is not watched",
                root,
                source.Name);
            return null;
        }
    }

    private void Disable()
    {
        lock (_lock)
        {
            DisposeWatchers();
            _enabled = false;
            _batch.Clear();
        }
    }

    private void DisposeWatchers()
    {
        foreach (FileSystemWatcher watcher in _watchers.Values)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    private void OnChanged(MediaSourceInfo source, string fullPath, WatcherChangeTypes change)
    {
        string relative = ScanScope.Normalize(Path.GetRelativePath(source.RootPath, fullPath));
        if (relative.StartsWith("..", StringComparison.Ordinal) || IgnoredNames.IsIgnoredPath(relative))
        {
            return;
        }

        string name = Path.GetFileName(fullPath);
        bool isMedia = new MediaKindClassifier(settings.Current).Classify(name) is not null;

        // Folders: created or deleted ones change their parent; changes of their content are reported for the files.
        bool isFolder = !isMedia && (change == WatcherChangeTypes.Deleted || Directory.Exists(fullPath));
        if (!isMedia && (!isFolder || change == WatcherChangeTypes.Changed))
        {
            return;
        }

        int slash = relative.LastIndexOf('/');
        lock (_lock)
        {
            if (_watchers.ContainsKey(source.Id))
            {
                _batch.Add(source.Id, slash < 0 ? string.Empty : relative[..slash], timeProvider.GetUtcNow());
            }
        }
    }

    private void OnError(MediaSourceInfo source, Exception exception)
    {
        logger.LogWarning(
            exception,
            "The file watcher missed changes; the whole media source {Source} is scanned",
            source.Name);
        lock (_lock)
        {
            if (_watchers.ContainsKey(source.Id))
            {
                _batch.RequestSource(source.Id, timeProvider.GetUtcNow());
            }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        ScanOptions scanOptions = options.Value;
        var interval = TimeSpan.FromMilliseconds(
            Math.Clamp(scanOptions.WatcherDebounce.TotalMilliseconds / 4, 20, 1000));
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, timeProvider, cancellationToken);
                await StartDueScanAsync(scanOptions, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The file watcher could not start a scan");
            }
        }
    }

    private async Task StartDueScanAsync(ScanOptions scanOptions, CancellationToken cancellationToken)
    {
        ScanScope scope;
        DateTimeOffset firstChange;
        lock (_lock)
        {
            if (!_batch.IsDue(timeProvider.GetUtcNow(), scanOptions.WatcherDebounce, scanOptions.WatcherMaxDelay)
                || tracker.GetState(JobIds.LibraryScan) != JobActivityState.Idle)
            {
                // A running scan does not see every change; the changes wait for the next scan.
                return;
            }

            firstChange = _batch.FirstChange!.Value;
            scope = _batch.Take();
        }

        JobRunStartResult result;
        try
        {
            await using AsyncServiceScope serviceScope = scopeFactory.CreateAsyncScope();
            IJobService jobs = serviceScope.ServiceProvider.GetRequiredService<IJobService>();
            result = await jobs.StartRunAsync(
                new JobRunRequest(
                    JobIds.LibraryScan,
                    JobRunTrigger.FileWatcher,
                    LibraryScanJob.CreatePayload(scope)),
                cancellationToken);
        }
        catch
        {
            Restore(scope, firstChange);
            throw;
        }

        if (!result.IsSuccess)
        {
            Restore(scope, firstChange);
            return;
        }

        logger.LogInformation(
            "Changes in {Folders} found by the file watcher, scan {RunId} started",
            string.Join(", ", scope.Folders.Select(folder => $"{folder.SourceId}:{folder.Path}")),
            result.Run.Id);
    }

    private void Restore(ScanScope scope, DateTimeOffset firstChange)
    {
        lock (_lock)
        {
            _batch.Restore(scope, firstChange);
        }
    }
}
