namespace WaveMP.Core.Organizing.Files;

/// <summary>
/// Finds out whether files can be created in the folder of a media source, e.g. not on a read-only mount.
/// </summary>
public interface IMediaWriteProbe
{
    MediaFolderState Probe(string rootPath);
}

/// <summary>
/// Whether the folder of a media source can be written.
/// </summary>
public enum MediaFolderState
{
    Writable,
    ReadOnly,
    Missing,
}
