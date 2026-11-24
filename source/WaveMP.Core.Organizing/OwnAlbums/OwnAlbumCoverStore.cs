using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Organizing.Options;

namespace WaveMP.Core.Organizing.OwnAlbums;

/// <summary>
/// The uploaded covers of the own albums in the folder <c>own-album-covers</c> of the data folder, as uploaded (JPEG,
/// PNG or WebP), one file <c>{id}.{ext}</c> per album. Applying an album copies the file as <c>cover.{ext}</c> into the
/// album folder. The folder holds data of the users and belongs into backups.
/// </summary>
public interface IOwnAlbumCoverStore
{
    /// <summary>
    /// Creates the folder and removes unfinished uploads. A folder that cannot be written does not fail.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognizes the picture by its content and stores it as the cover of the album, replacing an earlier one.
    /// </summary>
    Task<OwnAlbumCoverSave> SaveAsync(Guid albumId, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// The absolute path of a stored cover, or <c>null</c> if the file is missing.
    /// </summary>
    string? FindPath(string fileName);

    /// <summary>
    /// Removes a stored cover; a missing file is fine.
    /// </summary>
    void Delete(string fileName);

    /// <summary>
    /// Removes every stored cover except <paramref name="keep"/>.
    /// </summary>
    void RemoveOthers(IReadOnlySet<string> keep);
}

/// <summary>
/// The result of storing an own album cover.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="FileName">The stored file, if saved.</param>
/// <param name="ContentType">Its content type, if saved.</param>
public sealed record OwnAlbumCoverSave(OwnAlbumCoverSaveStatus Status, string? FileName, string? ContentType);

public enum OwnAlbumCoverSaveStatus
{
    Saved,
    UnsupportedFormat,
    Unavailable,
}

internal sealed class OwnAlbumCoverStore(IOptions<OrganizeOptions> options, ILogger<OwnAlbumCoverStore> logger)
    : IOwnAlbumCoverStore
{
    internal const string TemporaryPrefix = ".upload-";

    private static readonly Dictionary<string, string> _extensions = new(StringComparer.Ordinal)
    {
        [ImageFormats.Jpeg] = "jpg",
        [ImageFormats.Png] = "png",
        [ImageFormats.WebP] = "webp",
    };

    private string RootPath => options.Value.CoverPath;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(RootPath);
            foreach (string file in Directory.EnumerateFiles(RootPath, TemporaryPrefix + "*"))
            {
                File.Delete(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "The folder {Folder} of the own album covers cannot be written; covers cannot be uploaded",
                RootPath);
        }

        return Task.CompletedTask;
    }

    public async Task<OwnAlbumCoverSave> SaveAsync(
        Guid albumId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[ImageFormats.HeaderLength];
        int read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        string? contentType = ImageFormats.Detect(header.AsSpan(0, read));
        if (contentType is null || !_extensions.TryGetValue(contentType, out string? extension))
        {
            return new OwnAlbumCoverSave(OwnAlbumCoverSaveStatus.UnsupportedFormat, null, null);
        }

        string fileName = albumId.ToString("D") + "." + extension;
        string temporary = Path.Combine(RootPath, TemporaryPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(RootPath);
            await using (FileStream target = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await target.WriteAsync(header.AsMemory(0, read), cancellationToken);
                await content.CopyToAsync(target, cancellationToken);
                await target.FlushAsync(cancellationToken);
            }

            // Files of the data folder that belong to this album only; the media folder is never written here.
            File.Move(temporary, Path.Combine(RootPath, fileName), overwrite: true);
            foreach (string other in _extensions.Values.Where(other => other != extension))
            {
                Delete(albumId.ToString("D") + "." + other);
            }

            return new OwnAlbumCoverSave(OwnAlbumCoverSaveStatus.Saved, fileName, contentType);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Cannot store the cover of the own album {AlbumId}", albumId);
            Delete(Path.GetFileName(temporary));
            return new OwnAlbumCoverSave(OwnAlbumCoverSaveStatus.Unavailable, null, null);
        }
    }

    public string? FindPath(string fileName)
    {
        string path = Path.Combine(RootPath, Path.GetFileName(fileName));
        return File.Exists(path) ? path : null;
    }

    public void Delete(string fileName)
    {
        try
        {
            File.Delete(Path.Combine(RootPath, Path.GetFileName(fileName)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Cannot remove the own album cover {FileName}", fileName);
        }
    }

    public void RemoveOthers(IReadOnlySet<string> keep)
    {
        if (!Directory.Exists(RootPath))
        {
            return;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(RootPath))
            {
                string name = Path.GetFileName(file);
                if (!keep.Contains(name) && !name.StartsWith('.'))
                {
                    Delete(name);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Cannot list the folder {Folder} of the own album covers", RootPath);
        }
    }
}
