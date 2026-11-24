using System.Text;
using WaveMP.Core.Scanning.Discoveries;

namespace WaveMP.Core.Organizing.Files;

/// <summary>
/// Keeps every path of an organize operation inside its media folder: no rooted paths, no <c>.</c> or <c>..</c>, no
/// empty names, and no folder (or file) on the way that is a symbolic link, junction or other reparse point, so a
/// move can never reach files outside the media folder. Checked when the preview is made and again right before each
/// move.
/// </summary>
public static class MediaPathGuard
{
    private static readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// The absolute path of <paramref name="relativePath"/> (with <c>/</c>) inside <paramref name="rootPath"/>, if the
    /// path is safe: see the class. Parts of the path that do not exist yet are fine.
    /// </summary>
    /// <param name="rootPath">The folder of the media source.</param>
    /// <param name="relativePath">A path of a file or folder below it.</param>
    /// <param name="isTarget">Whether the path is to be created: then hidden and ignored names are refused as well, as
    /// the scan would not see the file.</param>
    /// <param name="fullPath">The absolute path.</param>
    public static bool TryResolve(string rootPath, string relativePath, bool isTarget, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrEmpty(relativePath) || relativePath.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = relativePath.Split('/');
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.Contains('\\', StringComparison.Ordinal)
                || (isTarget && (IgnoredNames.IsIgnored(segment) || !segment.IsNormalized(NormalizationForm.FormC))))
            {
                return false;
            }
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

        string current = root;
        foreach (string segment in segments)
        {
            current = Path.Combine(current, segment);
            if (IsLink(current))
            {
                return false;
            }
        }

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// Whether an existing file or folder is a symbolic link, junction or other reparse point; <c>false</c> if it does
    /// not exist.
    /// </summary>
    public static bool IsLink(string path)
    {
        try
        {
            FileSystemInfo? info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : File.Exists(path) ? new FileInfo(path) : null;
            if (info is null)
            {
                // A dangling link exists as an entry without a target.
                return new FileInfo(path).LinkTarget is not null;
            }

            return info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Unreadable entries are not trusted.
            return true;
        }
    }
}
