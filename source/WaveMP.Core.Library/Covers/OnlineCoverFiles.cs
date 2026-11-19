using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Library.Options;

namespace WaveMP.Core.Library.Covers;

internal sealed partial class OnlineCoverFiles(IOptions<OnlineCoverOptions> options, ILogger<OnlineCoverFiles> logger)
    : IOnlineCoverFiles
{
    private string RootPath => options.Value.RootPath;

    public string? GetFullPath(string fileName)
    {
        return FileNamePattern().IsMatch(fileName) ? Path.Combine(RootPath, fileName) : null;
    }

    public string? FileNameOf(Guid coverId, ReadOnlySpan<byte> picture)
    {
        string? extension = ImageFormats.Detect(picture) switch
        {
            ImageFormats.Jpeg => "jpg",
            ImageFormats.Png => "png",
            ImageFormats.WebP => "webp",
            ImageFormats.Gif => "gif",
            ImageFormats.Bmp => "bmp",
            _ => null,
        };
        return extension is null ? null : $"{coverId:N}.{extension}";
    }

    public async Task<bool> SaveAsync(
        string fileName,
        ReadOnlyMemory<byte> picture,
        CancellationToken cancellationToken = default)
    {
        string path = GetFullPath(fileName)
            ?? throw new ArgumentException("No file name of an online cover.", nameof(fileName));
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(RootPath);
            await File.WriteAllBytesAsync(temporary, picture, cancellationToken);
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The online cover {File} cannot be stored in {Folder}", fileName, RootPath);
            TryDelete(temporary);
            return false;
        }
    }

    public void Delete(string fileName)
    {
        if (GetFullPath(fileName) is { } path)
        {
            TryDelete(path);
        }
    }

    public IReadOnlyList<string> ListFileNames()
    {
        try
        {
            return Directory.Exists(RootPath)
                ? [.. Directory.EnumerateFiles(RootPath)
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Where(name => FileNamePattern().IsMatch(name))]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The folder {Folder} of the online covers cannot be listed", RootPath);
            return [];
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The online cover {File} cannot be removed", path);
        }
    }

    [GeneratedRegex("^[0-9a-f]{32}\\.(jpg|png|webp|gif|bmp)$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();
}
