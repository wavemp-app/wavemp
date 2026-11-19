namespace WaveMP.Core.Metadata.Files;

/// <summary>
/// Finds the files of the media sources by the source and the relative path the library stores.
/// </summary>
public interface IMediaFileLocator
{
    /// <summary>
    /// The absolute path of a file of a media source from its path relative to the folder of the source (with
    /// <c>/</c> as separator). Returns <c>false</c> for unknown sources, rooted paths and paths that leave the folder
    /// of the source (for example with <c>..</c>). Does not check whether the file exists.
    /// </summary>
    bool TryGetFullPath(Guid sourceId, string relativePath, out string fullPath);
}
