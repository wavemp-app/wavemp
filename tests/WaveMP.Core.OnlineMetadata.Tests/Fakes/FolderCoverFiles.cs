using WaveMP.Core.Library.Covers;

namespace WaveMP.Core.OnlineMetadata.Tests.Fakes;

/// <summary>
/// The online covers in a folder of the test.
/// </summary>
internal sealed class FolderCoverFiles(string root) : IOnlineCoverFiles
{
    public bool FailSaving { get; set; }

    public string? GetFullPath(string fileName)
    {
        return Path.Combine(root, fileName);
    }

    public string? FileNameOf(Guid coverId, ReadOnlySpan<byte> picture)
    {
        return ImageFormats.Detect(picture) is null ? null : $"{coverId:N}.jpg";
    }

    public async Task<bool> SaveAsync(
        string fileName,
        ReadOnlyMemory<byte> picture,
        CancellationToken cancellationToken = default)
    {
        if (FailSaving)
        {
            return false;
        }

        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(Path.Combine(root, fileName), picture.ToArray(), cancellationToken);
        return true;
    }

    public void Delete(string fileName)
    {
        File.Delete(Path.Combine(root, fileName));
    }

    public IReadOnlyList<string> ListFileNames()
    {
        return Directory.Exists(root) ? [.. Directory.EnumerateFiles(root).Select(Path.GetFileName)!] : [];
    }
}
