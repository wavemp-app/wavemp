using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.ArtistPictures.Options;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Caching.Images;
using WaveMP.Core.Library.Covers;

namespace WaveMP.Core.ArtistPictures.Stores;

internal sealed class ArtistPictureStore(
    ICoverImageEncoder encoder,
    IOptions<ArtistPictureOptions> options,
    ILogger<ArtistPictureStore> logger) : IArtistPictureStore
{
    internal const string TemporaryFolderName = ".tmp";
    internal const string FileExtension = ".webp";

    // Folders and files are named after the id of the picture only, formatted by the server: no name of an artist and
    // nothing a client sent ever becomes part of a path.
    internal const string FolderFormat = "N";

    private static readonly string[] _formats = [ImageFormats.Jpeg, ImageFormats.Png, ImageFormats.WebP];

    private volatile bool _isAvailable;

    public bool IsAvailable => _isAvailable;

    private string RootPath => options.Value.RootPath;

    private string TemporaryPath => Path.Combine(RootPath, TemporaryFolderName);

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(RootPath);
            if (Directory.Exists(TemporaryPath))
            {
                Directory.Delete(TemporaryPath, recursive: true);
            }

            Directory.CreateDirectory(TemporaryPath);
            _isAvailable = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _isAvailable = false;
            logger.LogWarning(
                exception,
                "The folder {Folder} of the artist pictures cannot be written; artist pictures cannot be stored",
                RootPath);
        }

        return Task.CompletedTask;
    }

    public Task<ArtistPictureSaveResult> SaveAsync(
        Guid pictureId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(pictureId, content, null, cancellationToken);
    }

    public async Task<ArtistPictureSaveResult> SaveAsync(
        Guid pictureId,
        Stream content,
        CoverCrop? crop,
        CancellationToken cancellationToken = default)
    {
        MemoryStream? copy = null;
        Stream picture = content;
        if (!content.CanSeek)
        {
            copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            picture = copy;
        }

        await using (copy)
        {
            return await SaveSeekableAsync(pictureId, picture, crop, cancellationToken);
        }
    }

    private async Task<ArtistPictureSaveResult> SaveSeekableAsync(
        Guid pictureId,
        Stream picture,
        CoverCrop? crop,
        CancellationToken cancellationToken)
    {
        picture.Position = 0;
        byte[] header = new byte[ImageFormats.HeaderLength];
        int read = await picture.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        string? contentType = ImageFormats.Detect(header.AsSpan(0, read));
        if (contentType is null || !_formats.Contains(contentType))
        {
            return ArtistPictureSaveResult.Failed(ArtistPictureSaveStatus.UnsupportedFormat);
        }

        if (!_isAvailable)
        {
            return ArtistPictureSaveResult.Failed(ArtistPictureSaveStatus.Unavailable);
        }

        picture.Position = 0;
        string prefix = Guid.NewGuid().ToString("N");
        CoverEncodeTarget[] targets = [.. CoverSizes.Stored.Select(size => new CoverEncodeTarget(
            size,
            Path.Combine(TemporaryPath, $"{prefix}-{size.ToString(CultureInfo.InvariantCulture)}{FileExtension}"),
            Square: true,
            Crop: crop))];
        try
        {
            CoverEncodeStatus status = await encoder.EncodeAsync(picture, contentType, targets, cancellationToken);
            switch (status)
            {
                case CoverEncodeStatus.Unavailable:
                    return ArtistPictureSaveResult.Failed(ArtistPictureSaveStatus.Unavailable);
                case CoverEncodeStatus.Failed:
                    return ArtistPictureSaveResult.Failed(ArtistPictureSaveStatus.Unreadable);
            }

            (int width, int height) = await ReadSizeAsync(targets[^1].OutputPath, cancellationToken);
            string folder = FolderOf(pictureId);
            Directory.CreateDirectory(folder);
            foreach (CoverEncodeTarget target in targets)
            {
                File.Move(target.OutputPath, FileOf(pictureId, target.MaxSize), overwrite: true);
            }

            return new ArtistPictureSaveResult(ArtistPictureSaveStatus.Saved, width, height);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Cannot store the artist picture {PictureId}", pictureId);
            Delete(pictureId);
            return ArtistPictureSaveResult.Failed(ArtistPictureSaveStatus.Unavailable);
        }
        finally
        {
            foreach (CoverEncodeTarget target in targets)
            {
                TryDeleteFile(target.OutputPath);
            }
        }
    }

    public Stream? Open(Guid pictureId, int size)
    {
        try
        {
            // Delete: the picture may be removed while it is delivered.
            return new FileStream(
                FileOf(pictureId, CoverSizes.SnapStored(size)),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                bufferSize: 1,
                useAsync: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Delete(Guid pictureId)
    {
        string folder = FolderOf(pictureId);
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Cannot remove the artist picture {PictureId}", pictureId);
        }
    }

    public IReadOnlyList<Guid> ListPictureIds()
    {
        if (!Directory.Exists(RootPath))
        {
            return [];
        }

        try
        {
            return [.. Directory.EnumerateDirectories(RootPath)
                .Select(Path.GetFileName)
                .Select(name => Guid.TryParseExact(name, FolderFormat, out Guid id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Cannot list the folder {Folder} of the artist pictures", RootPath);
            return [];
        }
    }

    /// <summary>
    /// The size of the largest stored file, from its WebP header.
    /// </summary>
    private static async Task<(int Width, int Height)> ReadSizeAsync(string path, CancellationToken cancellationToken)
    {
        byte[] header = new byte[WebPSizes.HeaderLength];
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        int read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        return WebPSizes.TryRead(header.AsSpan(0, read), out int width, out int height) ? (width, height) : (0, 0);
    }

    private string FolderOf(Guid pictureId)
    {
        return Path.Combine(RootPath, pictureId.ToString(FolderFormat));
    }

    private string FileOf(Guid pictureId, int size)
    {
        return Path.Combine(
            FolderOf(pictureId),
            $"picture-{size.ToString(CultureInfo.InvariantCulture)}{FileExtension}");
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "Cannot remove the unfinished picture {Path}", path);
        }
    }
}
