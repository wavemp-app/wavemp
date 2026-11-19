using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Library.Options;
using WaveMP.Core.Metadata.Files;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.OnlineMetadata.Covers;

internal sealed class OnlineCoverWriter(
    IOnlineMetadataRepository repository,
    IOnlineCoverFiles files,
    IMediaFileLocator locator,
    ILibraryScanner scanner,
    ISettingsSnapshot<LibrarySettings> librarySettings,
    IOptions<OnlineCoverOptions> coverOptions,
    TimeProvider timeProvider,
    ILogger<OnlineCoverWriter> logger) : IOnlineCoverWriter
{
    /// <summary>
    /// Folder in the folder of the online covers that keeps the pictures a cover from the online search replaced.
    /// </summary>
    internal const string ReplacedFolderName = "replaced";

    public async Task<CoverSaveOutcome> SaveAsync(
        CoverTarget target,
        byte[] picture,
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        string? extension = ExtensionOf(ImageFormats.Detect(picture), librarySettings.Current.ImageExtensions);
        if (extension is not null
            && WriteToMediaFolder(target, picture, extension) is { } written)
        {
            if (await repository.RemoveOnlineCoverAsync(target.AlbumId, target.TrackId, CancellationToken.None)
                is { } removed)
            {
                files.Delete(removed);
            }

            await RefreshFolderAsync(target, written);
            logger.LogInformation("Cover from the online search saved as {File}", written[0]);
            return CoverSaveOutcome.MediaFolder;
        }

        return await SaveToDataFolderAsync(target, picture, releaseId)
            ? CoverSaveOutcome.DataFolder
            : CoverSaveOutcome.NotSaved;
    }

    /// <summary>
    /// The extension of a picture of the type that the library reads (one of the image extensions of the settings);
    /// <c>null</c> if the library would not read it.
    /// </summary>
    internal static string? ExtensionOf(string? contentType, IReadOnlyList<string> imageExtensions)
    {
        string[] candidates = contentType switch
        {
            ImageFormats.Jpeg => ["jpg", "jpeg"],
            ImageFormats.Png => ["png"],
            ImageFormats.WebP => ["webp"],
            ImageFormats.Gif => ["gif"],
            ImageFormats.Bmp => ["bmp"],
            _ => [],
        };
        return candidates.FirstOrDefault(candidate =>
            imageExtensions.Contains(candidate, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The path of a file of the folder of the target relative to the folder of its source.
    /// </summary>
    internal static string RelativePathOf(string directoryPath, string fileName)
    {
        return directoryPath.Length == 0 ? fileName : $"{directoryPath}/{fileName}";
    }

    /// <summary>
    /// Writes the picture into the folder of the target. Returns the relative paths of the new picture (first) and of
    /// the replaced ones, or <c>null</c> if the folder cannot be written; then nothing is changed.
    /// </summary>
    private List<string>? WriteToMediaFolder(CoverTarget target, byte[] picture, string extension)
    {
        string fileName = $"{target.BaseName}.{extension}";
        string relativePath = RelativePathOf(target.DirectoryPath, fileName);
        if (!locator.TryGetFullPath(target.SourceId, relativePath, out string fullPath)
            || Path.GetDirectoryName(fullPath) is not { } folder
            || !Directory.Exists(folder))
        {
            return null;
        }

        // A hidden name: the scan and the file watcher skip names that start with a dot.
        string temporary = Path.Combine(folder, $".wavemp-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, picture);
            List<string> paths = [relativePath];
            string backupFolder = Path.Combine(
                coverOptions.Value.RootPath,
                ReplacedFolderName,
                timeProvider.GetUtcNow().ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture)
                    + "-" + Guid.NewGuid().ToString("N"));
            foreach (string existing in SamePictures(folder, target.BaseName))
            {
                Directory.CreateDirectory(backupFolder);
                File.Copy(existing, Path.Combine(backupFolder, Path.GetFileName(existing)), overwrite: true);
                if (!string.Equals(existing, fullPath, StringComparison.Ordinal))
                {
                    File.Delete(existing);
                    paths.Add(RelativePathOf(target.DirectoryPath, Path.GetFileName(existing)));
                }
            }

            File.Move(temporary, fullPath, overwrite: true);
            return paths;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogInformation(
                exception,
                "The cover {File} cannot be written into the media folder; it is kept in the data folder",
                relativePath);
            TryDelete(temporary);
            return null;
        }
    }

    /// <summary>
    /// The pictures of the folder with the base name (without regard to case), e.g. <c>cover.png</c> next to the new
    /// <c>cover.jpg</c>; they would compete with the new picture.
    /// </summary>
    private IEnumerable<string> SamePictures(string folder, string baseName)
    {
        IReadOnlyList<string> extensions = librarySettings.Current.ImageExtensions;
        return [.. Directory.EnumerateFiles(folder)
            .Where(path =>
                string.Equals(Path.GetFileNameWithoutExtension(path), baseName, StringComparison.OrdinalIgnoreCase)
                && extensions.Contains(Path.GetExtension(path).TrimStart('.'), StringComparer.OrdinalIgnoreCase))];
    }

    private async Task<bool> SaveToDataFolderAsync(CoverTarget target, byte[] picture, Guid releaseId)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        string? contentType = ImageFormats.Detect(picture);
        string? fileName = files.FileNameOf(id, picture);
        if (contentType is null || fileName is null || !await files.SaveAsync(fileName, picture))
        {
            return false;
        }

        (bool saved, string? replaced) = await repository.SetOnlineCoverAsync(
            new OnlineCover
            {
                Id = id,
                AlbumId = target.AlbumId,
                TrackId = target.AlbumId is null ? target.TrackId : null,
                FileName = fileName,
                MimeType = contentType,
                Size = picture.LongLength,
                ReleaseId = releaseId,
                CreatedAt = now,
            },
            CancellationToken.None);
        if (replaced is not null)
        {
            files.Delete(replaced);
        }

        if (!saved)
        {
            files.Delete(fileName);
            return false;
        }

        logger.LogInformation(
            "Cover from the online search kept in the data folder for {Owner}",
            target.AlbumId ?? target.TrackId);
        return true;
    }

    /// <summary>
    /// Reads the folder again, so the library knows the new picture at once. If that fails, the next scan finds it.
    /// </summary>
    private async Task RefreshFolderAsync(CoverTarget target, IReadOnlyCollection<string> changed)
    {
        try
        {
            await scanner.RefreshFolderAsync(target.SourceId, target.DirectoryPath, changed, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The folder {Folder} could not be read again after saving a cover; the next scan finds it",
                target.DirectoryPath);
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
            logger.LogWarning(exception, "The unfinished cover {File} cannot be removed", path);
        }
    }
}
