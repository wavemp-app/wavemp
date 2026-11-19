using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Lyrics.Options;
using WaveMP.Core.Metadata.Files;

namespace WaveMP.Core.Lyrics.Files;

/// <summary>
/// Lyrics files of the media sources, found through <see cref="IMediaFileLocator"/>, so a path never leaves the folder
/// of its source.
/// </summary>
internal sealed class LyricsFileStore(
    IMediaFileLocator locator,
    IOptions<LyricsOptions> options,
    ILogger<LyricsFileStore> logger) : ILyricsFileStore
{
    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<string?> ReadAsync(
        Guid sourceId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (!locator.TryGetFullPath(sourceId, relativePath, out string path))
        {
            return null;
        }

        try
        {
            FileInfo file = new(path);
            if (!file.Exists || file.Length > options.Value.MaxFileBytes)
            {
                return null;
            }

            return LyricsText.Decode(await File.ReadAllBytesAsync(path, cancellationToken));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The lyrics file {File} cannot be read", relativePath);
            return null;
        }
    }

    public async Task<LyricsFileWriteStatus> CreateAsync(
        Guid sourceId,
        string relativePath,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!locator.TryGetFullPath(sourceId, relativePath, out string path))
        {
            return LyricsFileWriteStatus.NotWritable;
        }

        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException) when (File.Exists(path))
        {
            return LyricsFileWriteStatus.Exists;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogInformation(exception, "The lyrics file {File} cannot be created", relativePath);
            return LyricsFileWriteStatus.NotWritable;
        }

        try
        {
            await using (stream)
            {
                await stream.WriteAsync(_utf8.GetBytes(text), cancellationToken);
            }

            return LyricsFileWriteStatus.Created;
        }
        catch (Exception exception)
        {
            // No half written file stays behind.
            TryDelete(path);
            if (exception is not (IOException or UnauthorizedAccessException))
            {
                throw;
            }

            logger.LogInformation(exception, "The lyrics file {File} cannot be written", relativePath);
            return LyricsFileWriteStatus.NotWritable;
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
            logger.LogWarning(exception, "The incomplete lyrics file {File} cannot be removed", path);
        }
    }
}
