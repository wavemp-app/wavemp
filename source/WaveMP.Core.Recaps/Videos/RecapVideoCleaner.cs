using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Recaps.Options;

namespace WaveMP.Core.Recaps.Videos;

/// <summary>
/// Deletes files in the folder of the recap videos that are older than <see cref="RecapOptions.TemporaryFileMaxAge"/>:
/// uploads and conversions left over by a restart or by a request that was aborted.
/// </summary>
internal sealed class RecapVideoCleaner(
    IOptions<RecapOptions> options,
    TimeProvider timeProvider,
    ILogger<RecapVideoCleaner> logger)
{
    /// <summary>
    /// Deletes the old files; returns how many were deleted.
    /// </summary>
    public int CleanUp()
    {
        RecapOptions settings = options.Value;
        if (!Directory.Exists(settings.VideoPath))
        {
            return 0;
        }

        DateTime limit = timeProvider.GetUtcNow().UtcDateTime - settings.TemporaryFileMaxAge;
        int deleted = 0;
        foreach (string file in Directory.EnumerateFiles(settings.VideoPath))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < limit)
                {
                    File.Delete(file);
                    deleted++;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "The temporary file {Path} could not be deleted", file);
            }
        }

        return deleted;
    }
}
