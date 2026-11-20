using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Caching.Images;
using WaveMP.Core.Library.Covers;

namespace WaveMP.Core.Users.Avatars;

internal sealed class AvatarStore(
    ICoverImageEncoder encoder,
    IOptions<AvatarOptions> options,
    ILogger<AvatarStore> logger) : IAvatarStore
{
    internal const string TemporaryFolderName = ".tmp";
    internal const string FileExtension = ".webp";

    private static readonly string[] _uploadFormats = [ImageFormats.Jpeg, ImageFormats.Png, ImageFormats.WebP];

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
                "The folder {Folder} of the avatars cannot be written; avatars cannot be uploaded",
                RootPath);
        }

        return Task.CompletedTask;
    }

    public async Task<AvatarSaveStatus> SaveAsync(
        Guid userId,
        Stream content,
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
            return await SaveSeekableAsync(userId, picture, cancellationToken);
        }
    }

    private async Task<AvatarSaveStatus> SaveSeekableAsync(
        Guid userId,
        Stream picture,
        CancellationToken cancellationToken)
    {
        picture.Position = 0;
        byte[] header = new byte[ImageFormats.HeaderLength];
        int read = await picture.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        string? contentType = ImageFormats.Detect(header.AsSpan(0, read));
        if (contentType is null || !_uploadFormats.Contains(contentType))
        {
            return AvatarSaveStatus.UnsupportedFormat;
        }

        if (!_isAvailable)
        {
            return AvatarSaveStatus.Unavailable;
        }

        picture.Position = 0;
        string prefix = Guid.NewGuid().ToString("N");
        CoverEncodeTarget[] targets = [.. CoverSizes.Stored.Select(size => new CoverEncodeTarget(
            size,
            Path.Combine(TemporaryPath, $"{prefix}-{size.ToString(CultureInfo.InvariantCulture)}{FileExtension}"),
            Square: true))];
        try
        {
            CoverEncodeStatus status = await encoder.EncodeAsync(picture, contentType, targets, cancellationToken);
            switch (status)
            {
                case CoverEncodeStatus.Unavailable:
                    return AvatarSaveStatus.Unavailable;
                case CoverEncodeStatus.Failed:
                    return AvatarSaveStatus.Unreadable;
            }

            Directory.CreateDirectory(FolderOf(userId));
            foreach (CoverEncodeTarget target in targets)
            {
                File.Move(target.OutputPath, FileOf(userId, target.MaxSize), overwrite: true);
            }

            return AvatarSaveStatus.Saved;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Cannot store the avatar of the profile {UserId}", userId);
            return AvatarSaveStatus.Unavailable;
        }
        finally
        {
            foreach (CoverEncodeTarget target in targets)
            {
                TryDeleteFile(target.OutputPath);
            }
        }
    }

    public Stream? Open(Guid userId, int size)
    {
        try
        {
            // Delete: a new upload may replace the file while it is delivered.
            return new FileStream(
                FileOf(userId, CoverSizes.SnapStored(size)),
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

    public void Delete(Guid userId)
    {
        string folder = FolderOf(userId);
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Cannot remove the avatar of the profile {UserId}", userId);
        }
    }

    public IReadOnlyList<Guid> ListUserIds()
    {
        if (!Directory.Exists(RootPath))
        {
            return [];
        }

        try
        {
            return [.. Directory.EnumerateDirectories(RootPath)
                .Select(Path.GetFileName)
                .Select(name => Guid.TryParseExact(name, "D", out Guid id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Cannot list the folder {Folder} of the avatars", RootPath);
            return [];
        }
    }

    private string FolderOf(Guid userId)
    {
        return Path.Combine(RootPath, userId.ToString("D"));
    }

    private string FileOf(Guid userId, int size)
    {
        return Path.Combine(
            FolderOf(userId),
            $"avatar-{size.ToString(CultureInfo.InvariantCulture)}{FileExtension}");
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
