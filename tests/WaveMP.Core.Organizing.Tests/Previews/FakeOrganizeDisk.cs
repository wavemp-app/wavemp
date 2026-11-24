using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Previews;

namespace WaveMP.Core.Organizing.Tests.Previews;

/// <summary>
/// A media folder in memory for the planner: files with size and time, folders, unsafe (linked) folders and read-only
/// files. Paths are relative with <c>/</c> and case-sensitive, like on Linux.
/// </summary>
internal sealed class FakeOrganizeDisk : IOrganizeDisk
{
    public static readonly DateTimeOffset Modified = new(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A root that does not exist, for the path checks of <see cref="MediaPathGuard"/>.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wavemp-fake-" + Guid.NewGuid().ToString("N"));

    private readonly Dictionary<string, FileFingerprint> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _folders = new(StringComparer.Ordinal);

    public HashSet<string> UnsafeFolders { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ReadOnlyFiles { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Files that have the same content as the uploaded cover.
    /// </summary>
    public HashSet<string> SameContent { get; } = new(StringComparer.Ordinal);

    public void AddFile(string relativePath, long size = 100, DateTimeOffset? modified = null)
    {
        _files[relativePath] = new FileFingerprint(size, modified ?? Modified);
        AddFolder(FolderNames.DirectoryOf(relativePath));
    }

    public void RemoveFile(string relativePath)
    {
        _files.Remove(relativePath);
    }

    public void AddFolder(string relativePath)
    {
        string current = relativePath;
        while (current.Length > 0)
        {
            _folders.Add(current);
            current = FolderNames.DirectoryOf(current);
        }
    }

    public IReadOnlyList<DiskEntry>? List(Guid sourceId, string directoryPath)
    {
        if (directoryPath.Length > 0 && !_folders.Contains(directoryPath))
        {
            return null;
        }

        return
        [
            .. _folders.Where(folder => folder != directoryPath && FolderNames.DirectoryOf(folder) == directoryPath)
                .Select(folder => new DiskEntry(FolderNames.FileNameOf(folder), true)),
            .. _files.Keys.Where(file => FolderNames.DirectoryOf(file) == directoryPath)
                .Select(file => new DiskEntry(FolderNames.FileNameOf(file), false)),
        ];
    }

    public FileFingerprint? Fingerprint(Guid sourceId, string relativePath)
    {
        return _files.GetValueOrDefault(relativePath);
    }

    public bool IsSafe(Guid sourceId, string relativePath, bool isTarget)
    {
        return MediaPathGuard.TryResolve(_root, relativePath, isTarget, out _)
            && !UnsafeFolders.Any(folder => relativePath.StartsWith(folder + "/", StringComparison.Ordinal));
    }

    public bool IsWritable(Guid sourceId, string relativePath)
    {
        return !ReadOnlyFiles.Contains(relativePath);
    }

    public bool HasSameContent(Guid sourceId, string relativePath, string otherFullPath)
    {
        return SameContent.Contains(relativePath);
    }
}
