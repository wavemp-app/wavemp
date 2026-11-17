namespace WaveMP.Core.Lyrics.Files;

/// <summary>
/// The result of creating a lyrics file.
/// </summary>
public enum LyricsFileWriteStatus
{
    Created,

    /// <summary>
    /// A file with the name already exists; it was not changed.
    /// </summary>
    Exists,

    /// <summary>
    /// The folder is not writable (read-only mount, missing permission) or gone.
    /// </summary>
    NotWritable,
}
