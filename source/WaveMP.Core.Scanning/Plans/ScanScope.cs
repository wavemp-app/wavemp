namespace WaveMP.Core.Scanning.Plans;

/// <summary>
/// What a scan covers: all active media sources, or some folders of some sources with all their subfolders (e.g. after
/// changes reported by the file watcher, or a whole source after the admin added it).
/// </summary>
public sealed class ScanScope
{
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<string>>? _sources;

    private ScanScope(IReadOnlyDictionary<Guid, IReadOnlyList<string>>? sources)
    {
        _sources = sources;
    }

    /// <summary>
    /// All active sources.
    /// </summary>
    public static ScanScope Full { get; } = new(null);

    public bool IsFull => _sources is null;

    /// <summary>
    /// The sources of a partial scope; empty for <see cref="Full"/>.
    /// </summary>
    public IReadOnlyCollection<Guid> SourceIds => _sources is null ? [] : [.. _sources.Keys];

    /// <summary>
    /// The folders of a partial scope, source by source; empty for <see cref="Full"/>. An empty path stands for the
    /// whole source.
    /// </summary>
    public IReadOnlyList<SourceFolder> Folders => _sources is null
        ? []
        : [.. _sources
            .OrderBy(source => source.Key)
            .SelectMany(source => source.Value.Select(path => new SourceFolder(source.Key, path)))];

    /// <summary>
    /// A scope of folders given as paths relative to the folder of their source (with <c>/</c> or <c>\</c>). Folders
    /// inside other folders of the same source are left out; an empty path makes it the whole source.
    /// </summary>
    public static ScanScope Of(IEnumerable<SourceFolder> folders)
    {
        var sources = folders
            .GroupBy(folder => folder.SourceId)
            .ToDictionary(group => group.Key, group => Reduce(group.Select(folder => folder.Path)));
        return new ScanScope(sources);
    }

    /// <summary>
    /// A scope of folders of one source.
    /// </summary>
    public static ScanScope Of(Guid sourceId, IEnumerable<string> directories)
    {
        string[] paths = [.. directories];
        return Of(paths.Length == 0
            ? [new SourceFolder(sourceId, string.Empty)]
            : paths.Select(path => new SourceFolder(sourceId, path)));
    }

    /// <summary>
    /// A scope of whole sources.
    /// </summary>
    public static ScanScope OfSources(IEnumerable<Guid> sourceIds)
    {
        return Of(sourceIds.Select(id => new SourceFolder(id, string.Empty)));
    }

    /// <summary>
    /// The folders of a source in the scope, without nested ones; a single empty path for the whole source (always for
    /// <see cref="Full"/>), <c>null</c> if the source is not part of the scope.
    /// </summary>
    public IReadOnlyList<string>? DirectoriesOf(Guid sourceId)
    {
        return _sources is null ? [string.Empty] : _sources.GetValueOrDefault(sourceId);
    }

    /// <summary>
    /// Whether a folder of a source lies in the scope.
    /// </summary>
    public bool Contains(Guid sourceId, string directory)
    {
        return DirectoriesOf(sourceId) is { } directories && directories.Any(scope => scope.Length == 0
            || directory == scope
            || directory.StartsWith(scope + "/", StringComparison.Ordinal));
    }

    public static string Normalize(string directory)
    {
        return string.Join('/', directory
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => segment != "."));
    }

    /// <summary>
    /// Normalized, sorted folders without nested ones; a single empty path if the folder of the source is one of them.
    /// </summary>
    private static IReadOnlyList<string> Reduce(IEnumerable<string> directories)
    {
        string[] normalized = [.. directories
            .Select(Normalize)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        if (normalized.Length == 0 || normalized.Contains(string.Empty))
        {
            return [string.Empty];
        }

        List<string> reduced = [];
        foreach (string directory in normalized)
        {
            if (!reduced.Any(parent => directory.StartsWith(parent + "/", StringComparison.Ordinal)))
            {
                reduced.Add(directory);
            }
        }

        return reduced;
    }
}
