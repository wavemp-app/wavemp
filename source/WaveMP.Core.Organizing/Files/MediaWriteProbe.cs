using Microsoft.Extensions.Logging;

namespace WaveMP.Core.Organizing.Files;

/// <summary>
/// Creates a new hidden file in the folder of the source (exclusively, so nothing existing is touched) and removes it
/// at once. Hidden names are ignored by the scan and the file watcher.
/// </summary>
internal sealed class MediaWriteProbe(ILogger<MediaWriteProbe> logger) : IMediaWriteProbe
{
    internal const string ProbePrefix = ".wavemp-write-check-";

    public MediaFolderState Probe(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return MediaFolderState.Missing;
        }

        string path = Path.Combine(rootPath, ProbePrefix + Guid.NewGuid().ToString("N"));
        try
        {
            using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.None))
            {
            }

            File.Delete(path);
            return MediaFolderState.Writable;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "The media folder {Folder} cannot be written", rootPath);
            return MediaFolderState.ReadOnly;
        }
    }
}
