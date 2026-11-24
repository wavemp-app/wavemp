using WaveMP.Core.Caching.Stores;

namespace WaveMP.Core.ArtistPictures.Tests.Fakes;

/// <summary>
/// A cache in a folder without size limit or index: enough to check what the lookup stores, reads and names.
/// </summary>
internal sealed class FolderCacheStore(string rootPath) : ICacheStore
{
    public bool IsAvailable { get; set; } = true;

    public string RootPath => rootPath;

    /// <summary>
    /// The names of all files of the area of the artist lookups.
    /// </summary>
    public IReadOnlyList<string> Names(CacheArea area = CacheArea.ArtistLookups)
    {
        string folder = GetAreaPath(area);
        return Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal)]
            : [];
    }

    public string GetAreaPath(CacheArea area)
    {
        return Path.Combine(rootPath, area.ToString());
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Stream? TryOpen(CacheArea area, string name)
    {
        string path = Path.Combine(GetAreaPath(area), name);
        return IsAvailable && File.Exists(path) ? File.OpenRead(path) : null;
    }

    public bool Contains(CacheArea area, string name)
    {
        return File.Exists(Path.Combine(GetAreaPath(area), name));
    }

    public string CreateTemporaryPath(string extension)
    {
        string folder = Path.Combine(rootPath, "tmp");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, Guid.NewGuid().ToString("N") + extension);
    }

    public string CreateTemporaryDirectory()
    {
        throw new NotSupportedException();
    }

    public void DeleteTemporaryDirectory(string path)
    {
        throw new NotSupportedException();
    }

    public bool Commit(CacheArea area, string name, string temporaryPath)
    {
        if (!IsAvailable)
        {
            DeleteTemporary(temporaryPath);
            return false;
        }

        string path = Path.Combine(GetAreaPath(area), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Move(temporaryPath, path, overwrite: true);
        return true;
    }

    public void DeleteTemporary(string temporaryPath)
    {
        File.Delete(temporaryPath);
    }

    public CacheUsage GetUsage()
    {
        throw new NotSupportedException();
    }

    public Task<int> RemoveAsync(
        CacheArea area,
        Func<string, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<int> TrimAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public void ScheduleTrim()
    {
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
