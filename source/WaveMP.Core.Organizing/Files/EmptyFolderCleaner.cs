namespace WaveMP.Core.Organizing.Files;

/// <summary>
/// Removes folders that a move left completely empty: starting at a folder and going up, each folder that has no
/// entry at all (also no hidden file) is removed, never the folder of the source itself, never a link, and never a
/// folder with anything left in it (the removal itself refuses non-empty folders).
/// </summary>
public static class EmptyFolderCleaner
{
    /// <summary>
    /// Removes <paramref name="directoryPath"/> and its parents while they are empty. Returns the removed folders
    /// relative to the source, innermost first.
    /// </summary>
    /// <param name="rootPath">The folder of the source.</param>
    /// <param name="directoryPath">A folder relative to it with <c>/</c>.</param>
    public static IReadOnlyList<string> RemoveEmptyFolders(string rootPath, string directoryPath)
    {
        List<string> removed = [];
        string current = directoryPath;
        while (current.Length > 0)
        {
            if (!MediaPathGuard.TryResolve(rootPath, current, isTarget: false, out string fullPath)
                || !Directory.Exists(fullPath))
            {
                break;
            }

            try
            {
                if (Directory.EnumerateFileSystemEntries(fullPath).Any())
                {
                    break;
                }

                // Not recursive: fails if anything appeared in the meantime.
                Directory.Delete(fullPath, recursive: false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                break;
            }

            removed.Add(current);
            current = FolderNames.DirectoryOf(current);
        }

        return removed;
    }
}
