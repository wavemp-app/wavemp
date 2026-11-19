using WaveMP.Core.Metadata.Sources;

namespace WaveMP.Core.Metadata.Files;

/// <summary>
/// Resolves relative paths against the folder of their media source. The result always lies inside the folder of the
/// source, so a stored path can never point to other files of the server.
/// </summary>
internal sealed class MediaFileLocator(IMediaSourceCatalog sources) : IMediaFileLocator
{
    private static readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public bool TryGetFullPath(Guid sourceId, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        return sources.Find(sourceId) is { } source && TryResolve(source.RootPath, relativePath, out fullPath);
    }

    /// <summary>
    /// The absolute path of <paramref name="relativePath"/> inside <paramref name="rootPath"/>; <c>false</c> for rooted
    /// paths and paths that leave the folder.
    /// </summary>
    public static bool TryResolve(string rootPath, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrEmpty(relativePath) || relativePath.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        string native = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(native))
        {
            return false;
        }

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        string candidate = Path.GetFullPath(Path.Combine(root, native));
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, _pathComparison))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }
}
