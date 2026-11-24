using WaveMP.Core.Metadata.Files;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Organizing.Files;

namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// The media folders as they are on disk, for one preview: folder listings are read once and kept.
/// </summary>
internal sealed class OrganizeDisk(IMediaSourceCatalog sources, IFileWriteAccess writeAccess) : IOrganizeDisk
{
    private readonly Dictionary<(Guid, string), IReadOnlyList<DiskEntry>?> _listings = [];

    public IReadOnlyList<DiskEntry>? List(Guid sourceId, string directoryPath)
    {
        if (_listings.TryGetValue((sourceId, directoryPath), out IReadOnlyList<DiskEntry>? cached))
        {
            return cached;
        }

        IReadOnlyList<DiskEntry>? entries = null;
        if (Resolve(sourceId, directoryPath, isTarget: false) is { } path && Directory.Exists(path))
        {
            try
            {
                entries = [.. new DirectoryInfo(path).EnumerateFileSystemInfos()
                    .Select(info => new DiskEntry(info.Name, info is DirectoryInfo))];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                entries = null;
            }
        }

        _listings[(sourceId, directoryPath)] = entries;
        return entries;
    }

    public FileFingerprint? Fingerprint(Guid sourceId, string relativePath)
    {
        if (Resolve(sourceId, relativePath, isTarget: false) is not { } path)
        {
            return null;
        }

        FileInfo info = new(path);
        return info.Exists
            ? new FileFingerprint(info.Length, FileFingerprint.Truncate(info.LastWriteTimeUtc))
            : null;
    }

    public bool IsSafe(Guid sourceId, string relativePath, bool isTarget)
    {
        return Resolve(sourceId, relativePath, isTarget) is not null;
    }

    public bool IsWritable(Guid sourceId, string relativePath)
    {
        return Resolve(sourceId, relativePath, isTarget: false) is { } path && writeAccess.IsWritable(path);
    }

    public bool HasSameContent(Guid sourceId, string relativePath, string otherFullPath)
    {
        if (Resolve(sourceId, relativePath, isTarget: false) is not { } path)
        {
            return false;
        }

        try
        {
            FileInfo left = new(path);
            FileInfo right = new(otherFullPath);
            if (!left.Exists || !right.Exists || left.Length != right.Length)
            {
                return false;
            }

            return File.ReadAllBytes(path).AsSpan().SequenceEqual(File.ReadAllBytes(otherFullPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string? Resolve(Guid sourceId, string relativePath, bool isTarget)
    {
        if (sources.Find(sourceId) is not { } source)
        {
            return null;
        }

        if (relativePath.Length == 0)
        {
            return source.RootPath;
        }

        return MediaPathGuard.TryResolve(source.RootPath, relativePath, isTarget, out string fullPath) ? fullPath : null;
    }
}
