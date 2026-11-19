using WaveMP.Core.Scanning.Plans;

namespace WaveMP.Core.Scanning.Watchers;

/// <summary>
/// Collects the folders changed in the media sources until they are scanned: once no change came in for the debounce
/// time, at the latest after the maximum delay since the first change. Not thread-safe.
/// </summary>
public sealed class MediaChangeBatch
{
    private readonly HashSet<SourceFolder> _folders = [];
    private DateTimeOffset? _lastChange;

    public bool IsEmpty => _folders.Count == 0;

    /// <summary>
    /// When the first change of the batch came in.
    /// </summary>
    public DateTimeOffset? FirstChange { get; private set; }

    /// <summary>
    /// Adds a changed folder of a source (relative path; empty for the whole source).
    /// </summary>
    public void Add(Guid sourceId, string directory, DateTimeOffset now)
    {
        _folders.Add(new SourceFolder(sourceId, ScanScope.Normalize(directory)));
        Touch(now);
    }

    /// <summary>
    /// Asks for a scan of a whole source, e.g. because the watcher missed events or the source is new.
    /// </summary>
    public void RequestSource(Guid sourceId, DateTimeOffset now)
    {
        Add(sourceId, string.Empty, now);
    }

    public bool IsDue(DateTimeOffset now, TimeSpan debounce, TimeSpan maxDelay)
    {
        return !IsEmpty && (now - _lastChange >= debounce || now - FirstChange >= maxDelay);
    }

    /// <summary>
    /// The folders to scan; the batch is empty afterwards.
    /// </summary>
    public ScanScope Take()
    {
        var scope = ScanScope.Of(_folders);
        Clear();
        return scope;
    }

    /// <summary>
    /// Puts the folders of a scan that could not start back, keeping the time of their first change.
    /// </summary>
    public void Restore(ScanScope scope, DateTimeOffset firstChange)
    {
        _folders.UnionWith(scope.Folders);
        FirstChange = FirstChange is { } first && first < firstChange ? first : firstChange;
        _lastChange ??= firstChange;
    }

    /// <summary>
    /// Leaves out the folders of sources that are no longer watched.
    /// </summary>
    public void Keep(IReadOnlySet<Guid> sourceIds)
    {
        _folders.RemoveWhere(folder => !sourceIds.Contains(folder.SourceId));
        if (IsEmpty)
        {
            Clear();
        }
    }

    public void Clear()
    {
        _folders.Clear();
        FirstChange = null;
        _lastChange = null;
    }

    private void Touch(DateTimeOffset now)
    {
        FirstChange ??= now;
        _lastChange = now;
    }
}
